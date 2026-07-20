using System.Threading.Tasks;
using Orleans;
using Aetherium.Model.Transit;

namespace Aetherium.Server.Transit
{
    /// <summary>
    /// Runs one scheduled transit service (add-transit-networks Phase 3): it owns a train — a boardable
    /// <c>IVehicleGrain</c> — and drives it around an ordered list of station docks, dwelling at each then
    /// departing to the next on a timed voyage, looping. It reuses the boardable-vehicles voyage machinery
    /// wholesale (the train's own <c>DepartAsync</c>/<c>ArriveAsync</c>/reminder); boarding and alighting
    /// are the unchanged <c>board</c>/<c>disembark</c> path on the parked train. Keyed by a service id.
    /// </summary>
    public interface ITransitServiceGrain : IGrainWithStringKey
    {
        /// <summary>Stores the line config, initializes the train (its interior), lands it at the first
        /// stop, and starts the dwell-then-depart cycle. Idempotent: a second call on a started service is
        /// a no-op.</summary>
        Task InitializeAsync(TransitServiceConfig config);

        /// <summary>One dispatch step (called by the service reminder, and directly by tests): advances the
        /// train exactly one transition — arrive an in-flight train, or, when dwelling and due, depart it to
        /// the next stop (looping). No-op until <see cref="InitializeAsync"/> has started the service.</summary>
        Task DispatchStepAsync();

        /// <summary>The train's grain id (a boardable <c>IVehicleGrain</c>), or null before start.</summary>
        Task<string?> GetTrainIdAsync();

        Task<TransitServiceInfo> GetInfoAsync();
    }
}
