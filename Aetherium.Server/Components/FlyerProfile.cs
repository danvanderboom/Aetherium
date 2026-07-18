using Aetherium.Core;

namespace Aetherium.Components
{
    /// <summary>
    /// Per-flyer interaction profile: what a flyer <em>is</em> (its kind) and which altitude-aware affordances
    /// it offers a player or agent. Reach differs by affordance so interactions respect altitude naturally: an
    /// uplink (hack) or a signal (summon/hail) reaches a flyer at any band within a planar range, while a weapon
    /// (attack/shoot) additionally requires the flyer to be within a small band delta — so a grounded player can
    /// shoot a low drone but can only hack an orbital satellite. All values are per-flyer data, attached at spawn.
    /// </summary>
    public class FlyerProfile : Component
    {
        /// <summary>Descriptive kind (e.g. "satellite", "air-taxi", "drone", "bird", "aircraft").</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>Uplink affordance (hack): reaches the flyer at any band within <see cref="UplinkRange"/>.</summary>
        public bool Hackable { get; set; }
        public int UplinkRange { get; set; } = 256;

        /// <summary>Signal affordance (summon/hail): reaches the flyer at any band within <see cref="SignalRange"/>.</summary>
        public bool Summonable { get; set; }
        public int SignalRange { get; set; } = 64;

        /// <summary>Weapon affordance (attack/shoot): requires planar range AND a small band delta to the flyer.</summary>
        public bool Attackable { get; set; }
        public int WeaponRange { get; set; } = 5;
        public int MaxReachBandDelta { get; set; } = 3;

        public FlyerProfile() : base() { }
    }
}
