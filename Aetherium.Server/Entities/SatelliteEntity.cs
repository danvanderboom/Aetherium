using Aetherium.Core;

namespace Aetherium.Entities
{
    /// <summary>
    /// A satellite in orbit. Deliberately <b>not</b> a <see cref="Aetherium.Character"/>: it must not be
    /// swept by the NPC/flight-plan/recognition/death systems (which iterate <c>world.Characters</c>), and
    /// it must be invisible to ordinary perception — a satellite is detectable only over a radio channel
    /// (see the radio perception path). It carries <see cref="Aetherium.Components.OrbitPath"/> (its orbit),
    /// <see cref="Aetherium.Components.Flight"/> (its altitude band), and a satellite
    /// <see cref="Aetherium.Components.FlyerProfile"/> (so it can be hacked when overhead).
    /// </summary>
    public class SatelliteEntity : Entity
    {
        public SatelliteEntity() : base() { }
    }
}
