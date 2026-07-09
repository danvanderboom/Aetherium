using System.Collections.Generic;

namespace Aetherium.Server.Combat
{
    public enum DamageDelivery
    {
        Melee,
        Ranged,
        Aoe,
    }

    /// <summary>
    /// One damage type/amount pair within a <see cref="DamagePacket"/>. Tags are data-driven per
    /// campaign (fantasy: slashing/piercing/fire/cold/arcane; sci-fi: kinetic/plasma/emp/radiation)
    /// — the engine core knows nothing about specific tag values.
    /// </summary>
    public readonly struct DamageComponent
    {
        public string Tag { get; }
        public double Amount { get; }

        public DamageComponent(string tag, double amount)
        {
            Tag = tag;
            Amount = amount;
        }
    }

    /// <summary>
    /// The currency of combat depth (engine gap-analysis §4.2): a bundle of typed damage
    /// components plus provenance, replacing the single flat integer the original melee MVP used.
    /// </summary>
    public class DamagePacket
    {
        public IReadOnlyList<DamageComponent> Components { get; }
        public string? SourceEntityId { get; }
        public DamageDelivery Delivery { get; }
        public IReadOnlyList<string> Tags { get; }

        public DamagePacket(IReadOnlyList<DamageComponent> components, string? sourceEntityId = null,
            DamageDelivery delivery = DamageDelivery.Melee, IReadOnlyList<string>? tags = null)
        {
            Components = components;
            SourceEntityId = sourceEntityId;
            Delivery = delivery;
            Tags = tags ?? System.Array.Empty<string>();
        }

        public static DamagePacket Single(string tag, double amount, string? sourceEntityId = null,
            DamageDelivery delivery = DamageDelivery.Melee)
            => new(new[] { new DamageComponent(tag, amount) }, sourceEntityId, delivery);
    }
}
