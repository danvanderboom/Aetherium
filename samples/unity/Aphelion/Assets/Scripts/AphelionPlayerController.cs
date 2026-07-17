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
        private AphelionCameraRig _cameraRig;
        private bool _busy;
        private bool _sunlight;

        private void Awake()
        {
            _behaviour = GetComponent<AetheriumClientBehaviour>();
            _cameraRig = FindAnyObjectByType<AphelionCameraRig>();
        }

        private static float HeadingFor(WorldDirection direction) => direction switch
        {
            WorldDirection.North => 0f,
            WorldDirection.East => 90f,
            WorldDirection.South => 180f,
            WorldDirection.West => 270f,
            _ => 0f,
        };

        private void Update()
        {
            if (_busy || _behaviour.Client == null)
                return;

            // WASD: absolute compass moves (the client composes the rotate+step for you).
            // Predict the facing locally so the camera orbits instantly instead of waiting for
            // the authoritative frame (the WASD rotate always lands, so no rollback).
            if (Input.GetKeyDown(KeyCode.W)) MoveCompass(WorldDirection.North);
            else if (Input.GetKeyDown(KeyCode.S)) MoveCompass(WorldDirection.South);
            else if (Input.GetKeyDown(KeyCode.A)) MoveCompass(WorldDirection.West);
            else if (Input.GetKeyDown(KeyCode.D)) MoveCompass(WorldDirection.East);
            // Arrows: the engine's native relative verbs — turn 90° and step along your heading.
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) TurnInPlace(clockwise: false);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) TurnInPlace(clockwise: true);
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
                      $"inViewCount={inView.Count}\n" + LocalLightMap(store, 6));
        }

        /// <summary>
        /// ASCII map of the anchor's neighborhood from the store: light level as a digit
        /// (0–9, in-view), 'o' remembered-but-out-of-view, '#' walls, '.' unknown. When a
        /// light-dead collapse happens, this shows whether adjacent cells are lit (~7 =
        /// vision problem) or dark (light problem), and what the terrain there is called.
        /// </summary>
        private static string LocalLightMap(PerceptionStore store, int radius)
        {
            var anchor = store.Anchor;
            var byPos = store.Memory
                .Where(c => c.Position.Z == anchor.Z)
                .ToDictionary(c => (c.Position.X, c.Position.Y));

            var rows = new System.Text.StringBuilder();
            var terrains = new System.Collections.Generic.HashSet<string>();
            for (var y = anchor.Y - radius; y <= anchor.Y + radius; y++)
            {
                for (var x = anchor.X - radius; x <= anchor.X + radius; x++)
                {
                    if (!byPos.TryGetValue((x, y), out var cell) || cell.Terrain == null)
                    {
                        rows.Append('.');
                        continue;
                    }
                    terrains.Add(cell.Terrain.Name);
                    if (cell.Terrain.Name == "Wall") { rows.Append('#'); continue; }
                    if (!cell.InView) { rows.Append('o'); continue; }
                    var digit = Mathf.Clamp(Mathf.RoundToInt((float)cell.LastLightLevel * 9f), 0, 9);
                    rows.Append(x == anchor.X && y == anchor.Y ? '@' : (char)('0' + digit));
                }
                rows.Append('\n');
            }
            rows.Append("terrains seen: " + string.Join(", ", terrains));
            return rows.ToString();
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

        /// <summary>Arrow turn: predict the 90° orbit immediately, roll back if rejected.</summary>
        private async void TurnInPlace(bool clockwise)
        {
            _busy = true;
            _cameraRig?.PredictTurn(clockwise);
            try
            {
                var result = await _behaviour.Client.Tools.RotateAsync(clockwise);
                if (!result.Success)
                    _cameraRig?.RollbackTurn(clockwise);
            }
            catch (System.Exception exception)
            {
                _cameraRig?.RollbackTurn(clockwise);
                Debug.LogWarning($"[Aphelion] Rotate failed: {exception.Message}");
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>WASD move: predict the compass facing immediately (the composite move's
        /// rotate always lands, so no rollback), then issue the real rotate+step.</summary>
        private async void MoveCompass(WorldDirection direction)
        {
            _busy = true;
            _cameraRig?.PredictHeadingTo(HeadingFor(direction));
            try
            {
                await _behaviour.Client.Tools.MoveAsync(direction);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[Aphelion] Move failed: {exception.Message}");
            }
            finally
            {
                _busy = false;
            }
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
