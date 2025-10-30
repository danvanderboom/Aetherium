using System;

namespace ConsoleGame.WorldGen.Algorithms.Noise
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
        /// <returns>Noise value in range [-1, 1]</returns>
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
            
            return total / maxValue;
        }

        /// <summary>
        /// Gets noise value normalized to [0, 1] range (useful for thresholding).
        /// </summary>
        public double NoiseNormalized(double x, double y)
        {
            return (Noise(x, y) + 1) / 2.0;
        }

        /// <summary>
        /// Gets fractal noise normalized to [0, 1] range.
        /// </summary>
        public double FractalNoiseNormalized(double x, double y, int octaves = 4, double persistence = 0.5, double lacunarity = 2.0)
        {
            return (FractalNoise(x, y, octaves, persistence, lacunarity) + 1) / 2.0;
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
    }
}

