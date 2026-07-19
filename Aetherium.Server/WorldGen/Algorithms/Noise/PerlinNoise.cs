using System;

namespace Aetherium.WorldGen.Algorithms.Noise
{
    /// <summary>
    /// Classic Perlin noise implementation for natural terrain generation.
    /// Deterministic based on seed for reproducible procedural content.
    /// </summary>
    public class PerlinNoise
    {
        private readonly int[] _permutation;
        private const int PermutationSize = 256;

        public PerlinNoise(int seed)
        {
            var random = new Random(seed);
            _permutation = new int[PermutationSize * 2];
            
            // Initialize permutation table
            var p = new int[PermutationSize];
            for (int i = 0; i < PermutationSize; i++)
            {
                p[i] = i;
            }
            
            // Shuffle using seed
            for (int i = PermutationSize - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (p[i], p[j]) = (p[j], p[i]);
            }
            
            // Duplicate for easier wrapping
            for (int i = 0; i < PermutationSize * 2; i++)
            {
                _permutation[i] = p[i % PermutationSize];
            }
        }

        /// <summary>
        /// Gets noise value at specified 2D coordinates.
        /// </summary>
        /// <param name="x">X coordinate (not limited to 0-1 range)</param>
        /// <param name="y">Y coordinate (not limited to 0-1 range)</param>
        /// <returns>
        /// Noise value, empirically in approximately [-0.71, 0.71] for classic 2D Perlin
        /// (gradient set is 8 directions with magnitude up to √2/2 after bilinear blend).
        /// Callers should not assume a strict [-1, 1] range when thresholding. Use
        /// <see cref="NoiseNormalized"/> for a [0, 1] range that maps the empirical
        /// midpoint to 0.5.
        /// </returns>
        public double Noise(double x, double y)
        {
            // Find unit grid cell containing point
            int X = (int)Math.Floor(x) & 255;
            int Y = (int)Math.Floor(y) & 255;
            
            // Get relative xy coordinates within cell
            x -= Math.Floor(x);
            y -= Math.Floor(y);
            
            // Compute fade curves for x and y
            double u = Fade(x);
            double v = Fade(y);
            
            // Hash coordinates of the 4 corners
            int aa = _permutation[_permutation[X] + Y];
            int ab = _permutation[_permutation[X] + Y + 1];
            int ba = _permutation[_permutation[X + 1] + Y];
            int bb = _permutation[_permutation[X + 1] + Y + 1];
            
            // Blend results from 4 corners
            double result = Lerp(v,
                Lerp(u, Grad(aa, x, y), Grad(ba, x - 1, y)),
                Lerp(u, Grad(ab, x, y - 1), Grad(bb, x - 1, y - 1)));
            
            return result;
        }

        /// <summary>
        /// Gets noise value at specified 3D coordinates. Needed for spherical worlds (H3): a
        /// planet's terrain is sampled over each cell's centre unit vector (x, y, z) on the sphere,
        /// which has no seams or pole singularities the way a (latitude, longitude) plane does.
        /// Empirical range and thresholding caveats match the 2D <see cref="Noise(double, double)"/>.
        /// Uses Ken Perlin's improved-noise 3D gradient over the same seeded permutation table, so a
        /// given seed is reproducible.
        /// </summary>
        public double Noise(double x, double y, double z)
        {
            // Find unit grid cube containing point.
            int X = (int)Math.Floor(x) & 255;
            int Y = (int)Math.Floor(y) & 255;
            int Z = (int)Math.Floor(z) & 255;

            // Relative coordinates within cube.
            x -= Math.Floor(x);
            y -= Math.Floor(y);
            z -= Math.Floor(z);

            // Fade curves.
            double u = Fade(x);
            double v = Fade(y);
            double w = Fade(z);

            // Hash the 8 cube corners.
            int a = _permutation[X] + Y;
            int aa = _permutation[a] + Z;
            int ab = _permutation[a + 1] + Z;
            int b = _permutation[X + 1] + Y;
            int ba = _permutation[b] + Z;
            int bb = _permutation[b + 1] + Z;

            // Blend the 8 corner gradients.
            return Lerp(w,
                Lerp(v,
                    Lerp(u, Grad(_permutation[aa], x, y, z),
                            Grad(_permutation[ba], x - 1, y, z)),
                    Lerp(u, Grad(_permutation[ab], x, y - 1, z),
                            Grad(_permutation[bb], x - 1, y - 1, z))),
                Lerp(v,
                    Lerp(u, Grad(_permutation[aa + 1], x, y, z - 1),
                            Grad(_permutation[ba + 1], x - 1, y, z - 1)),
                    Lerp(u, Grad(_permutation[ab + 1], x, y - 1, z - 1),
                            Grad(_permutation[bb + 1], x - 1, y - 1, z - 1))));
        }

        /// <summary>
        /// Gets fractal noise with multiple octaves for more detail.
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="octaves">Number of noise layers to combine (more = more detail)</param>
        /// <param name="persistence">How much each octave contributes (typically 0.5)</param>
        /// <param name="lacunarity">Frequency multiplier per octave (typically 2.0)</param>
        /// <returns>Fractal noise value, normalized to approximately [-1, 1]</returns>
        public double FractalNoise(double x, double y, int octaves = 4, double persistence = 0.5, double lacunarity = 2.0)
        {
            if (octaves <= 0)
                return 0.0;

            double total = 0;
            double frequency = 1;
            double amplitude = 1;
            double maxValue = 0;  // For normalization

            for (int i = 0; i < octaves; i++)
            {
                total += Noise(x * frequency, y * frequency) * amplitude;

                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            // Guard against degenerate maxValue (e.g., persistence = 0 with octaves > 1 collapses
            // amplitude to 0 after the first iteration; maxValue would be 1.0 in that case, but
            // for safety we never divide by zero).
            return maxValue > 0 ? total / maxValue : 0.0;
        }

        /// <summary>
        /// Gets 3D fractal noise with multiple octaves — the spherical-worldgen counterpart of the
        /// 2D overload. Normalized to approximately [-1, 1].
        /// </summary>
        public double FractalNoise(double x, double y, double z, int octaves = 4, double persistence = 0.5, double lacunarity = 2.0)
        {
            if (octaves <= 0)
                return 0.0;

            double total = 0;
            double frequency = 1;
            double amplitude = 1;
            double maxValue = 0;

            for (int i = 0; i < octaves; i++)
            {
                total += Noise(x * frequency, y * frequency, z * frequency) * amplitude;

                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return maxValue > 0 ? total / maxValue : 0.0;
        }

        /// <summary>
        /// Gets noise value normalized to [0, 1] range (useful for thresholding).
        /// </summary>
        public double NoiseNormalized(double x, double y)
        {
            return (Noise(x, y) + 1) / 2.0;
        }

        /// <summary>
        /// Gets 3D noise value normalized to [0, 1] range.
        /// </summary>
        public double NoiseNormalized(double x, double y, double z)
        {
            return (Noise(x, y, z) + 1) / 2.0;
        }

        /// <summary>
        /// Gets fractal noise normalized to [0, 1] range.
        /// </summary>
        public double FractalNoiseNormalized(double x, double y, int octaves = 4, double persistence = 0.5, double lacunarity = 2.0)
        {
            return (FractalNoise(x, y, octaves, persistence, lacunarity) + 1) / 2.0;
        }

        /// <summary>
        /// Gets 3D fractal noise normalized to [0, 1] range — used to sample a planet's elevation
        /// and moisture over the sphere's cell-centre unit vectors.
        /// </summary>
        public double FractalNoiseNormalized(double x, double y, double z, int octaves = 4, double persistence = 0.5, double lacunarity = 2.0)
        {
            return (FractalNoise(x, y, z, octaves, persistence, lacunarity) + 1) / 2.0;
        }

        private static double Fade(double t)
        {
            // Smoothstep interpolation: 6t^5 - 15t^4 + 10t^3
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        private static double Lerp(double t, double a, double b)
        {
            return a + t * (b - a);
        }

        private static double Grad(int hash, double x, double y)
        {
            // Convert low 4 bits of hash into 8 gradient directions
            int h = hash & 7;
            double u = h < 4 ? x : y;
            double v = h < 4 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        private static double Grad(int hash, double x, double y, double z)
        {
            // Ken Perlin's improved-noise 3D gradient: low 4 bits pick one of 12 edge directions.
            int h = hash & 15;
            double u = h < 8 ? x : y;
            double v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}


