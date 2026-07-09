namespace Aetherium.Server.Combat
{
    /// <summary>Pure helper composing a <see cref="DamagePacket"/> with a target's <see cref="Resistances"/>.</summary>
    public static class DamageResolution
    {
        /// <summary>Sums each component's post-mitigation amount. <paramref name="resistances"/> may be
        /// null (an entity with no Resistances component takes damage unmitigated).</summary>
        public static double ResolveTotal(DamagePacket packet, Resistances? resistances)
        {
            double total = 0;
            foreach (var component in packet.Components)
            {
                total += resistances is null
                    ? System.Math.Max(0, component.Amount)
                    : resistances.Mitigate(component.Tag, component.Amount);
            }
            return total;
        }
    }
}
