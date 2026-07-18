using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Client;
using Aetherium.Client.Contracts;
using Aetherium.Unity;
using Aphelion;
using UnityEngine;

namespace Overworld
{
    /// <summary>
    /// Input driver for the open-world sandbox: WASD moves by compass (the client composes the
    /// rotate+step the server requires), arrows turn/step along the heading, and <b>E</b>
    /// interacts — picking up an adjacent item (e.g. a key), opening/closing an adjacent door,
    /// or unlocking a locked door with a key you're carrying. There are no monsters, so there
    /// is no attack; instead the world defaults to <b>daylight</b> (press <b>L</b> to toggle the
    /// carried lamp), because most of the map is meant to be seen under the sun.
    ///
    /// <para>Doors and keys are driven off perception <see cref="AffordanceDto"/>s: the server
    /// only offers an "open"/"use" affordance when you're actually in reach, so E just acts on
    /// whatever affordance is present. Picked-up keys are remembered by shape so a later locked
    /// door can be matched to the right key.</para>
    /// </summary>
    [RequireComponent(typeof(AetheriumClientBehaviour))]
    public sealed class OverworldPlayerController : MonoBehaviour
    {
        private AetheriumClientBehaviour _behaviour;
        private AphelionCameraRig _cameraRig;
        private bool _busy;
        private bool _sunlight;
        private bool _daylightApplied;

        // Keys we've picked up, by lock shape → the key's entity id (still valid in inventory),
        // so a locked door's RequiresKeyId can be matched to the key that opens it.
        private readonly Dictionary<string, string> _carriedKeys = new Dictionary<string, string>();

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
            // Once frames are flowing (so the connection is live), switch the world to daylight —
            // this is a sunlit sandbox, unlike Aphelion's dark stations.
            if (!_daylightApplied && _behaviour.Client != null && _behaviour.Store?.LatestFrame != null)
            {
                _daylightApplied = true;
                _sunlight = true;
                Fire(_behaviour.Client.Tools.SetLightingModeAsync(LightingMode.Sunlight));
            }

            if (_busy || _behaviour.Client == null)
                return;

            if (Input.GetKeyDown(KeyCode.W)) MoveCompass(WorldDirection.North);
            else if (Input.GetKeyDown(KeyCode.S)) MoveCompass(WorldDirection.South);
            else if (Input.GetKeyDown(KeyCode.A)) MoveCompass(WorldDirection.West);
            else if (Input.GetKeyDown(KeyCode.D)) MoveCompass(WorldDirection.East);
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) TurnInPlace(clockwise: false);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) TurnInPlace(clockwise: true);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) Fire(_behaviour.Client.Tools.MoveForwardAsync());
            else if (Input.GetKeyDown(KeyCode.DownArrow)) Fire(_behaviour.Client.Tools.MoveBackwardAsync());
            else if (Input.GetKeyDown(KeyCode.E)) Interact();
            else if (Input.GetKeyDown(KeyCode.L)) ToggleLighting();
        }

        /// <summary>E: pick up a nearby item, else open/close a door, else unlock one with a
        /// carried key — whichever the current perception frame makes available.</summary>
        private void Interact()
        {
            var store = _behaviour.Store;
            var frame = store?.LatestFrame;
            if (store == null || frame == null)
                return;

            // 1. An adjacent item (the key) — pick it up, and remember it by lock shape.
            var anchor = store.Anchor;
            TrackedEntity nearestItem = null;
            var best = int.MaxValue;
            foreach (var entity in store.Entities)
            {
                if (!entity.IsItem)
                    continue;
                if (entity.Position.Z != anchor.Z)
                    continue;
                var d = Mathf.Max(Mathf.Abs(entity.Position.X - anchor.X), Mathf.Abs(entity.Position.Y - anchor.Y));
                if (d <= 1 && d < best) { best = d; nearestItem = entity; }
            }
            if (nearestItem != null)
            {
                var keyShape = nearestItem.Item?.KeyId;
                if (!string.IsNullOrEmpty(keyShape))
                    _carriedKeys[keyShape] = nearestItem.Id;
                Fire(_behaviour.Client.Tools.PickupAsync(nearestItem.Id));
                return;
            }

            // 2. A door in reach — the affordance carries the door's entity id.
            var affordances = frame.Affordances ?? new List<AffordanceDto>();
            var open = affordances.FirstOrDefault(a => Is(a.Action, "open") && a.TargetId != null);
            if (open != null) { Fire(_behaviour.Client.Tools.OpenAsync(open.TargetId)); return; }

            var close = affordances.FirstOrDefault(a => Is(a.Action, "close") && a.TargetId != null);
            if (close != null) { Fire(_behaviour.Client.Tools.CloseAsync(close.TargetId)); return; }

            // 3. A locked door we hold the key for.
            var locked = affordances.FirstOrDefault(a =>
                Is(a.Action, "use") && a.TargetId != null && !string.IsNullOrEmpty(a.RequiresKeyId));
            if (locked != null && _carriedKeys.TryGetValue(locked.RequiresKeyId, out var keyEntityId))
            {
                var usageId = locked.UsageOptions
                    .FirstOrDefault(u => u.UsageId != null && u.UsageId.Contains("unlock"))?.UsageId ?? "unlock-door";
                Fire(_behaviour.Client.Tools.UseAsync(keyEntityId, locked.TargetId, usageId));
                return;
            }

            if (locked != null)
                Debug.Log($"[Overworld] That door needs a '{locked.RequiresKeyId}' key — find it first.");
        }

        private static bool Is(string action, string verb) =>
            action != null && action.Equals(verb, System.StringComparison.OrdinalIgnoreCase);

        private void ToggleLighting()
        {
            _sunlight = !_sunlight;
            Debug.Log($"[Overworld] Lighting: {(_sunlight ? "Daylight" : "Carried lamp")}");
            Fire(_behaviour.Client.Tools.SetLightingModeAsync(
                _sunlight ? LightingMode.Sunlight : LightingMode.Torch));
        }

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
                Debug.LogWarning($"[Overworld] Rotate failed: {exception.Message}");
            }
            finally { _busy = false; }
        }

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
                Debug.LogWarning($"[Overworld] Move failed: {exception.Message}");
            }
            finally { _busy = false; }
        }

        private async void Fire<T>(Task<T> request)
        {
            _busy = true;
            try { await request; }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[Overworld] Action failed: {exception.Message}");
            }
            finally { _busy = false; }
        }
    }
}
