using Aetherium.Client.Contracts;
using Aetherium.Unity;
using UnityEngine;

namespace Aphelion
{
    /// <summary>
    /// Interoception HUD (add-interoception-channel): renders the player's own body state
    /// from the perception frame's Interoception block — health bar, a red flash when
    /// health drops, and downed/respawn/death banners from the player-lifecycle events.
    /// Before this existed, combat was invisible: a vent-lurker attacking from outside
    /// the directional FOV (or beyond the lamp) could down and kill a player who never
    /// saw a single indication they were being hit.
    /// </summary>
    [RequireComponent(typeof(AetheriumClientBehaviour))]
    public sealed class AphelionVitalsHud : MonoBehaviour
    {
        [Tooltip("Seconds a lifecycle banner (downed/respawned) stays on screen.")]
        [SerializeField] private float bannerSeconds = 4f;
        [Tooltip("Seconds the damage flash takes to fade out.")]
        [SerializeField] private float flashFadeSeconds = 0.6f;

        private AetheriumClientBehaviour _behaviour;
        private int _lastHealth = -1;
        private float _flashUntil;
        private string _banner;
        private float _bannerUntil;
        private Texture2D _white;

        private void Awake()
        {
            _behaviour = GetComponent<AetheriumClientBehaviour>();
            _behaviour.Downed += v => ShowBanner($"DOWN — suit integrity failing ({v.Health}/{v.MaxHealth})");
            _behaviour.Respawned += v => ShowBanner("Reconstructed at the docking bay");
            _behaviour.Died += _ => ShowBanner("Signal lost");
            _white = Texture2D.whiteTexture;
        }

        private void ShowBanner(string text)
        {
            _banner = text;
            _bannerUntil = Time.time + bannerSeconds;
            Debug.Log($"[Aphelion][vitals] {text}");
        }

        private void Update()
        {
            var self = _behaviour.Store?.LatestFrame?.Interoception;
            if (self == null)
                return;

            if (_lastHealth >= 0 && self.Health < _lastHealth)
            {
                _flashUntil = Time.time + flashFadeSeconds;
                Debug.Log($"[Aphelion][vitals] Hit! {_lastHealth} -> {self.Health}/{self.MaxHealth}");
            }
            _lastHealth = self.Health;
        }

        private void OnGUI()
        {
            var self = _behaviour.Store?.LatestFrame?.Interoception;

            // Damage flash: a red vignette that fades over flashFadeSeconds.
            var flashLeft = _flashUntil - Time.time;
            if (flashLeft > 0f)
            {
                GUI.color = new Color(1f, 0f, 0f, 0.35f * (flashLeft / flashFadeSeconds));
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _white);
                GUI.color = Color.white;
            }

            // Health bar, bottom-left. Interoception is null until the first frame arrives
            // (or against a server that predates the channel) — draw nothing rather than lie.
            if (self != null && self.MaxHealth > 0)
            {
                const float w = 260f, h = 18f, pad = 16f;
                var back = new Rect(pad, Screen.height - h - pad, w, h);
                var frac = Mathf.Clamp01((float)self.Health / self.MaxHealth);

                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(back, _white);
                GUI.color = frac > 0.5f ? new Color(0.2f, 0.85f, 0.4f)
                          : frac > 0.25f ? new Color(0.95f, 0.75f, 0.2f)
                          : new Color(0.9f, 0.2f, 0.2f);
                GUI.DrawTexture(new Rect(back.x + 2, back.y + 2, (w - 4) * frac, h - 4), _white);
                GUI.color = Color.white;
                GUI.Label(new Rect(back.x + 6, back.y - 1, w, h),
                    $"{self.Health}/{self.MaxHealth}");
            }

            // Lifecycle banner, top-center.
            if (!string.IsNullOrEmpty(_banner) && Time.time < _bannerUntil)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                };
                var rect = new Rect(0, Screen.height * 0.18f, Screen.width, 40f);
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(Screen.width * 0.25f, rect.y, Screen.width * 0.5f, rect.height), _white);
                GUI.color = Color.white;
                GUI.Label(rect, _banner, style);
            }
        }
    }
}
