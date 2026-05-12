using System;
using Aetherium.Components;
using Aetherium.Server;
using Aetherium.Server.MultiWorld;
using Aetherium.WorldBuilders;
using Xunit;

namespace Aetherium.Test.MultiWorld
{
    /// <summary>
    /// Verifies GameSession.ApplyDelta correctly reconciles its local
    /// HeatTrailTracker mirror in response to HeatRecordedDelta and
    /// HeatExpiredDelta. The grain is authoritative for heat; sessions only
    /// receive it via deltas (add-grain-heat-trails).
    /// </summary>
    public class SessionHeatMirrorTests
    {
        private static GameSession NewSession() =>
            new GameSession("heat-test", new FovDiagnosticWorldBuilder("open_space"));

        /// <summary>
        /// Computes the GameTimeHours value that corresponds to "right now" in the
        /// session's clock. Heat deltas use this so that recorded trails have a
        /// timestamp close to the session's `GetCurrentGameTime()` — otherwise the
        /// default TimeScale of 60x causes a 10-second heat duration to expire in
        /// ~167ms of real time and the assertion races against the clock.
        /// </summary>
        private static double NowGameTimeHours(GameSession session)
            => (session.GetCurrentGameTime() - session.GameStartTime).TotalHours;

        [Fact]
        public void HeatRecordedDelta_For_Known_Entity_Updates_Mirror()
        {
            var session = NewSession();
            var playerId = session.Player!.EntityId;

            session.ApplyDelta(new HeatRecordedDelta
            {
                EntityId = playerId,
                X = 12, Y = 13, Z = 0,
                GameTimeHours = NowGameTimeHours(session),
                Intensity = 0.5,
            });

            var loc = new WorldLocation(12, 13, 0);
            var heat = session.HeatTracker.GetHeatAtLocation(loc, session.GetCurrentGameTime());
            Assert.True(heat > 0, $"Expected heat > 0 at {loc}, got {heat}");
        }

        [Fact]
        public void HeatRecordedDelta_For_Unknown_Entity_Still_Records()
        {
            var session = NewSession();

            session.ApplyDelta(new HeatRecordedDelta
            {
                EntityId = "ghost-entity-not-in-mirror",
                X = 5, Y = 5, Z = 0,
                GameTimeHours = NowGameTimeHours(session),
                Intensity = 0.8,
            });

            var loc = new WorldLocation(5, 5, 0);
            var heat = session.HeatTracker.GetHeatAtLocation(loc, session.GetCurrentGameTime());
            Assert.True(heat > 0, "Heat should be recorded even for entities not in the local mirror");
        }

        [Fact]
        public void HeatExpiredDelta_Removes_Trails_At_Cell()
        {
            var session = NewSession();
            session.ApplyDelta(new HeatRecordedDelta
            {
                EntityId = session.Player!.EntityId,
                X = 7, Y = 7, Z = 0,
                GameTimeHours = NowGameTimeHours(session),
                Intensity = 0.6,
            });
            var loc = new WorldLocation(7, 7, 0);
            Assert.True(session.HeatTracker.GetHeatAtLocation(loc, session.GetCurrentGameTime()) > 0);

            session.ApplyDelta(new HeatExpiredDelta { X = 7, Y = 7, Z = 0 });

            Assert.Equal(0, session.HeatTracker.GetHeatAtLocation(loc, session.GetCurrentGameTime()));
        }

        [Fact]
        public void GetPerception_No_Longer_Self_Collects_Heat()
        {
            // Before add-grain-heat-trails, GetPerception called UpdateHeatTracker
            // which iterated World.Entities. Now heat flows only via deltas. Calling
            // GetPerception on a session that just contains the player should NOT
            // produce any heat data at the player's location.
            var session = NewSession();
            var perception = session.GetPerception();
            Assert.NotNull(perception);

            var playerLoc = session.ViewLocation!;
            var heat = session.HeatTracker.GetHeatAtLocation(playerLoc, session.GetCurrentGameTime());
            Assert.Equal(0, heat);
        }
    }
}
