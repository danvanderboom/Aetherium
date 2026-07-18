using Aetherium.Core;

namespace Aetherium.Entities
{
    /// <summary>
    /// A settlement placed on the map — carries a <see cref="Aetherium.Components.Settlement"/> at its
    /// core cell. Kept a distinct entity type (like <see cref="LightEntity"/>) so it reads clearly in
    /// scans and can grow its own behaviour (markets, garrisons) without overloading Character.
    /// </summary>
    public class SettlementEntity : Entity
    {
        public SettlementEntity() : base() { }
    }
}
