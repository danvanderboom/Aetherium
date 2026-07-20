using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Runtime;
using Aetherium.Model.Transit;
using Aetherium.Server.Vehicles;

namespace Aetherium.Server.Transit
{
    /// <summary>
    /// Runs one scheduled transit service (add-transit-networks Phase 3). Modeled on <c>VehicleGrain</c>'s
    /// own reminder-driven voyage: this grain owns a train (an <see cref="IVehicleGrain"/> keyed off the
    /// service id) and walks it around the line's stops, dwelling then departing, on its own 1-minute
    /// dispatch reminder. It adds no travel mechanics of its own — every hop is the train's existing
    /// <see cref="IVehicleGrain.DepartAsync"/> + arrival, so a passenger who boarded the parked train at
    /// one station rides the interior to the next and alights there with the ordinary <c>disembark</c>.
    /// Auto-discovered by Orleans; reuses the "worldStore" provider.
    /// </summary>
    public class TransitServiceGrain : Grain, ITransitServiceGrain, IRemindable
    {
        private const string DispatchReminderName = "transit-dispatch";

        private readonly IPersistentState<TransitServiceState> _state;
        private readonly IGrainFactory _grainFactory;

        public TransitServiceGrain(
            [PersistentState("transit-service", "worldStore")] IPersistentState<TransitServiceState> state,
            IGrainFactory grainFactory)
        {
            _state = state;
            _grainFactory = grainFactory;
        }

        /// <summary>The train grain id for a service instance (stable across reactivation).</summary>
        public static string TrainIdFor(string serviceId) => "transit-train:" + serviceId;

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            await base.OnActivateAsync(cancellationToken);
            // A service that was running when the grain last deactivated re-arms its dispatch reminder so
            // the line keeps moving without a fresh InitializeAsync.
            if (_state.State.Started)
                await TryArmDispatchReminderAsync();
        }

        public async Task InitializeAsync(TransitServiceConfig config)
        {
            if (config is null)
                throw new ArgumentNullException(nameof(config));
            if (_state.State.Started)
                return; // idempotent

            _state.State.Config = config;
            _state.State.TrainId = TrainIdFor(this.GetPrimaryKeyString());
            _state.State.CurrentStopIndex = 0;
            _state.State.PendingStopIndex = -1;

            if (config.Stops.Count == 0)
            {
                // Nothing to run; store the (empty) config so a later re-init can replace it.
                await _state.WriteStateAsync();
                return;
            }

            // Stand up the train and park it at the first stop.
            var train = _grainFactory.GetGrain<IVehicleGrain>(_state.State.TrainId);
            await train.InitializeAsync(config.Train);

            var first = config.Stops[0];
            var landed = await train.LandAsync(first.DockWorldId, first.DockMapId, first.AnchorX, first.AnchorY, first.AnchorZ);
            if (!landed.Success)
            {
                // Couldn't dock at the origin — leave the service un-started; a retry can re-init.
                await _state.WriteStateAsync();
                return;
            }

            // Begin dwelling at stop 0; the first due departure leaves for stop 1.
            _state.State.NextDepartUtc = DateTime.UtcNow.AddMinutes(Math.Max(0.0, config.DwellMinutes));
            _state.State.Started = true;
            await _state.WriteStateAsync();

            await TryArmDispatchReminderAsync();
        }

        public async Task DispatchStepAsync()
        {
            var s = _state.State;
            if (!s.Started || s.Config is null || string.IsNullOrEmpty(s.TrainId) || s.Config.Stops.Count < 1)
                return;

            var train = _grainFactory.GetGrain<IVehicleGrain>(s.TrainId);

            // --- In transit: nudge the train's voyage (self-sufficient even without the train's own
            //     reminder), and if it has arrived, latch the new current stop and start its dwell. ---
            if (s.PendingStopIndex >= 0)
            {
                await train.TickVoyageAsync();
                var info = await train.GetInfoAsync();
                if (!info.InTransit && info.Landed)
                {
                    s.CurrentStopIndex = s.PendingStopIndex;
                    s.PendingStopIndex = -1;
                    s.NextDepartUtc = DateTime.UtcNow.AddMinutes(Math.Max(0.0, s.Config.DwellMinutes));
                    await _state.WriteStateAsync();
                }
                return;
            }

            // --- Dwelling at a stop: depart to the next one when the dwell is up. ---
            if (s.Config.Stops.Count < 2)
                return; // a single-stop line is stationary

            if (s.NextDepartUtc is null)
            {
                s.NextDepartUtc = DateTime.UtcNow.AddMinutes(Math.Max(0.0, s.Config.DwellMinutes));
                await _state.WriteStateAsync();
                return;
            }

            if (DateTime.UtcNow < s.NextDepartUtc.Value)
                return; // still dwelling

            int next = NextStopIndex(s.CurrentStopIndex, s.Config.Stops.Count, s.Config.Loop);
            if (next < 0)
                return; // terminus of a non-looping line — the train stays put

            var dest = s.Config.Stops[next];
            var voyage = await train.DepartAsync(dest.DockWorldId, dest.DockMapId, dest.AnchorX, dest.AnchorY, dest.AnchorZ, s.Config.HopMinutes);
            if (voyage.Success)
            {
                s.PendingStopIndex = next;
                s.NextDepartUtc = null;
                await _state.WriteStateAsync();
            }
            // If the depart was rejected (e.g. dock momentarily blocked), retry on the next step.
        }

        private static int NextStopIndex(int current, int count, bool loop)
        {
            if (count < 2)
                return -1;
            if (current + 1 < count)
                return current + 1;
            return loop ? 0 : -1;
        }

        public Task ReceiveReminder(string reminderName, TickStatus status)
            => reminderName == DispatchReminderName ? DispatchStepAsync() : Task.CompletedTask;

        private async Task TryArmDispatchReminderAsync()
        {
            try
            {
                await this.RegisterOrUpdateReminder(DispatchReminderName, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            }
            catch (Exception ex)
            {
                // No reminder service (e.g. a headless/test host without one): the service still advances
                // when DispatchStepAsync is driven externally; it just won't self-schedule.
                Console.WriteLine($"[TransitServiceGrain] dispatch reminder unavailable: {ex.Message}");
            }
        }

        public Task<string?> GetTrainIdAsync() => Task.FromResult(_state.State.TrainId);

        public Task<TransitServiceInfo> GetInfoAsync()
        {
            var s = _state.State;
            var stops = s.Config?.Stops;
            string currentName = stops is not null && s.CurrentStopIndex >= 0 && s.CurrentStopIndex < stops.Count
                ? stops[s.CurrentStopIndex].Name
                : string.Empty;

            return Task.FromResult(new TransitServiceInfo
            {
                LineId = s.Config?.LineId ?? string.Empty,
                TrainId = s.TrainId,
                StopCount = stops?.Count ?? 0,
                CurrentStopIndex = s.CurrentStopIndex,
                CurrentStopName = currentName,
                InTransit = s.PendingStopIndex >= 0,
                Started = s.Started,
            });
        }
    }

    /// <summary>Persisted state for a <see cref="TransitServiceGrain"/>.</summary>
    [GenerateSerializer]
    public class TransitServiceState
    {
        [Id(0)] public TransitServiceConfig? Config { get; set; }
        [Id(1)] public string? TrainId { get; set; }
        [Id(2)] public int CurrentStopIndex { get; set; }
        /// <summary>The stop the train is currently voyaging toward, or -1 when docked/dwelling.</summary>
        [Id(3)] public int PendingStopIndex { get; set; } = -1;
        /// <summary>When the train may next depart the current stop (set on arrival = now + dwell).</summary>
        [Id(4)] public DateTime? NextDepartUtc { get; set; }
        [Id(5)] public bool Started { get; set; }
    }
}
