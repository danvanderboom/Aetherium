namespace Aetherium.Model
{
    /// <summary>
    /// Pure "context tint" policy (add-adaptive-depth-visualization 5.4): the default lighting mode for an
    /// altitude band, so underground bands read as enclosed/torch-lit and skyways as sunlit. Reuses the existing
    /// <see cref="LightingMode"/> values — no new rendering machinery. Callers decide whether to apply it.
    /// </summary>
    public static class BandContext
    {
        /// <summary>
        /// Suggested lighting mode for a band: below ground (band &lt; 0) → <see cref="LightingMode.Torch"/>
        /// (enclosed cueing); at/above <paramref name="skyThreshold"/> → <see cref="LightingMode.Sunlight"/>
        /// (skyway); the surface band(s) in between → <see cref="LightingMode.Ambient"/>.
        /// </summary>
        public static LightingMode SuggestLightingMode(int band, int skyThreshold = 1)
        {
            if (band < 0)
                return LightingMode.Torch;
            if (band >= skyThreshold)
                return LightingMode.Sunlight;
            return LightingMode.Ambient;
        }
    }
}
