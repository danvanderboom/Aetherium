using Aetherium.Core;

namespace Aetherium.Entities
{
    /// <summary>
    /// A transit station placed on the map — carries a <see cref="Aetherium.Components.Station"/> at a
    /// line's stop cell. A distinct entity type (like <see cref="SettlementEntity"/>) so it reads clearly
    /// in scans and can grow its own behaviour (timetable boards, ticketing) without overloading Character.
    /// </summary>
    public class StationEntity : Entity
    {
        public StationEntity() : base() { }
    }
}
