using System.Collections.Generic;

namespace Aetherium.Model
{
    /// <summary>
    /// The interoception channel (openspec/changes/add-interoception-channel): a character's
    /// awareness of their own body, carried as a perception sense alongside sight and hearing —
    /// not "HUD data". Self-only by construction: it reflects the perceiving character's own
    /// components and never another entity's internals (reading *others* is the separate,
    /// deliberately-banded social-insight channel, engine gap G2). Null on frames computed
    /// without a perceiving self (legacy callers), which keeps the wire additive.
    /// </summary>
    public class InteroceptionDto
    {
        /// <summary>The felt integrity of the body. 0/0 when the self has no health component.</summary>
        public int Health { get; set; }

        public int MaxHealth { get; set; }

        /// <summary>Felt status effects — you can tell that you are burning or slowed, and
        /// roughly how long it will last.</summary>
        public List<SelfStatusDto> Statuses { get; set; } = new List<SelfStatusDto>();

        /// <summary>Every resource pool this body carries (charge, oxygen, heat…).</summary>
        public List<ResourcePoolStateDto> Pools { get; set; } = new List<ResourcePoolStateDto>();

        /// <summary>Abilities still cooling down. A ready ability is simply absent — the
        /// read is "what isn't ready yet", which is exactly what a HUD dims.</summary>
        public List<AbilityReadinessDto> Cooldowns { get; set; } = new List<AbilityReadinessDto>();
    }

    /// <summary>One felt status effect on the perceiver's own body.</summary>
    public class SelfStatusDto
    {
        public string Id { get; set; } = "";
        public int RemainingTicks { get; set; }
    }

    /// <summary>
    /// One of the perceiver's own resource pools. <see cref="IsInverse"/> distinguishes a
    /// draining battery (spend empties it, regen refills) from a filling heat gauge (use fills
    /// it, venting drains) — without it a client renders the meter backwards.
    /// </summary>
    public class ResourcePoolStateDto
    {
        public string Tag { get; set; } = "";
        public double Current { get; set; }
        public double Max { get; set; }
        public bool IsInverse { get; set; }
    }

    /// <summary>One ability still on cooldown for the perceiver.</summary>
    public class AbilityReadinessDto
    {
        public string AbilityId { get; set; } = "";
        public int RemainingTicks { get; set; }
    }
}
