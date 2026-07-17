using System.Linq;
using System.Threading.Tasks;
using Aetherium.Client;
using Aetherium.Client.Contracts;
using Aetherium.Unity;
using UnityEngine;

namespace Aphelion
{
    /// <summary>
    /// M0 "First Light" input driver: WASD moves in world directions (the client composes
    /// rotate + step — the server only accepts relative movement, by design), Space attacks
    /// the nearest adjacent creature. One request in flight at a time; the server is
    /// authoritative and perception frames drive everything visible.
    /// </summary>
    [RequireComponent(typeof(AetheriumClientBehaviour))]
    public sealed class AphelionPlayerController : MonoBehaviour
    {
        private AetheriumClientBehaviour _behaviour;
        private bool _busy;
        private bool _sunlight;

        private void Awake()
        {
            _behaviour = GetComponent<AetheriumClientBehaviour>();
        }

        private void Update()
        {
            if (_busy || _behaviour.Client == null)
                return;

            // WASD: absolute compass moves (the client composes the rotate+step for you).
            if (Input.GetKeyDown(KeyCode.W)) Fire(_behaviour.Client.Tools.MoveAsync(WorldDirection.North));
            else if (Input.GetKeyDown(KeyCode.S)) Fire(_behaviour.Client.Tools.MoveAsync(WorldDirection.South));
            else if (Input.GetKeyDown(KeyCode.A)) Fire(_behaviour.Client.Tools.MoveAsync(WorldDirection.West));
            else if (Input.GetKeyDown(KeyCode.D)) Fire(_behaviour.Client.Tools.MoveAsync(WorldDirection.East));
            // Arrows: the engine's native relative verbs — turn 90° and step along your heading.
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) Fire(_behaviour.Client.Tools.RotateAsync(clockwise: false));
            else if (Input.GetKeyDown(KeyCode.RightArrow)) Fire(_behaviour.Client.Tools.RotateAsync(clockwise: true));
            else if (Input.GetKeyDown(KeyCode.UpArrow)) Fire(_behaviour.Client.Tools.MoveForwardAsync());
            else if (Input.GetKeyDown(KeyCode.DownArrow)) Fire(_behaviour.Client.Tools.MoveBackwardAsync());
            else if (Input.GetKeyDown(KeyCode.Space)) AttackNearestAdjacent();
            else if (Input.GetKeyDown(KeyCode.L)) ToggleLighting();
            else if (Input.GetKeyDown(KeyCode.M)) DumpPerceptionDiagnostic();
        }

        /// <summary>
        /// Debug (M): logs the store's view of the lamp — the anchor must be the brightest
        /// in-view cell (the lamp is on you). If this logs MATCH while the on-screen pool
        /// looks off-center, the artifact is presentation/perspective; if it logs MISMATCH,
        /// the store data is wrong on this machine and the values identify how.
        /// </summary>
        private void DumpPerceptionDiagnostic()
        {
            var store = _behaviour.Store;
            if (store == null || store.LatestFrame == null)
            {
                Debug.Log("[Aphelion][M] no frame yet");
                return;
            }

            var anchor = store.Anchor;
            var inView = store.Memory
                .Where(c => c.Position.Z == anchor.Z && c.Terrain != null && c.InView)
                .ToList();
            if (inView.Count == 0)
            {
                Debug.Log("[Aphelion][M] nothing in view");
                return;
            }

            var brightest = inView.OrderByDescending(c => c.LastLightLevel).First();
            var west = anchor.X - inView.Min(c => c.Position.X);
            var east = inView.Max(c => c.Position.X) - anchor.X;
            var north = anchor.Y - inView.Min(c => c.Position.Y);
            var south = inView.Max(c => c.Position.Y) - anchor.Y;
            var verdict = brightest.Position == anchor ? "MATCH" : "** MISMATCH **";

            Debug.Log($"[Aphelion][M] anchor=({anchor.X},{anchor.Y},{anchor.Z}) " +
                      $"brightest=({brightest.Position.X},{brightest.Position.Y}) light={brightest.LastLightLevel:F2} {verdict} | " +
                      $"in-view extent W{west} E{east} N{north} S{south} | " +
                      $"seq={store.LatestFrame.MoveSequence} heading={store.LatestFrame.HeadingDegrees} " +
                      $"inViewCount={inView.Count}");
        }

        /// <summary>
        /// Debug: the world is dark by design (suit lamp, range ~6 cells) — sight is gated
        /// by light, not line of sight. L flips to server-side sunlight to see the layout.
        /// </summary>
        private void ToggleLighting()
        {
            _sunlight = !_sunlight;
            Debug.Log($"[Aphelion] Lighting: {(_sunlight ? "Sunlight (debug)" : "Torch (suit lamp)")}");
            Fire(_behaviour.Client.Tools.SetLightingModeAsync(
                _sunlight ? LightingMode.Sunlight : LightingMode.Torch));
        }

        private void AttackNearestAdjacent()
        {
            var store = _behaviour.Store;
            if (store == null)
                return;

            var anchor = store.Anchor;
            TrackedEntity target = null;
            var best = int.MaxValue;
            foreach (var entity in store.Entities)
            {
                if (entity.IsItem || entity.WasDefeated || entity.CreatureTypeId == null)
                    continue;
                var dx = Mathf.Abs(entity.Position.X - anchor.X);
                var dy = Mathf.Abs(entity.Position.Y - anchor.Y);
                var chebyshev = Mathf.Max(dx, dy);
                if (chebyshev <= 1 && chebyshev < best)
                {
                    best = chebyshev;
                    target = entity;
                }
            }

            if (target != null)
                Fire(_behaviour.Client.Tools.AttackAsync(target.Id));
        }

        private async void Fire<T>(Task<T> request)
        {
            _busy = true;
            try
            {
                await request;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[Aphelion] Action failed: {exception.Message}");
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
