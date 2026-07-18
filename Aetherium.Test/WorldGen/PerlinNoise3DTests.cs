using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.WorldGen.Algorithms.Noise;

namespace Aetherium.Test.WorldGen
{
    /// <summary>
    /// Verifies the 3-D Perlin noise added for spherical worldgen (docs/design/h3-sphere-worldgen.md):
    /// it is deterministic per seed, stays in range, is spatially varied (continuous, not constant),
    /// and is sensitive to the seed. Sampling in 3-D over cell-centre unit vectors is what lets an H3
    /// planet be seamless — no date-line discontinuity, no pole singularity.
    /// </summary>
    [TestFixture]
    public class PerlinNoise3DTests
    {
        private static IEnumerable<(double X, double Y, double Z)> UnitSphereSamples(int n = 200)
        {
            // Deterministic spread of points over the unit sphere (golden-angle spiral).
            const double golden = 2.399963229728653; // π(3−√5)
            for (int i = 0; i < n; i++)
            {
                double z = 1 - 2.0 * (i + 0.5) / n;
                double r = Math.Sqrt(Math.Max(0, 1 - z * z));
                double theta = golden * i;
                yield return (r * Math.Cos(theta), r * Math.Sin(theta), z);
            }
        }

        [Test]
        public void IsDeterministicForAGivenSeed()
        {
            var a = new PerlinNoise(7);
            var b = new PerlinNoise(7);
            foreach (var (x, y, z) in UnitSphereSamples())
            {
                Assert.That(b.Noise(x * 1.7, y * 1.7, z * 1.7),
                    Is.EqualTo(a.Noise(x * 1.7, y * 1.7, z * 1.7)).Within(1e-12));
                Assert.That(b.FractalNoiseNormalized(x, y, z, 5),
                    Is.EqualTo(a.FractalNoiseNormalized(x, y, z, 5)).Within(1e-12));
            }
        }

        [Test]
        public void NormalizedNoiseStaysInUnitRange()
        {
            var noise = new PerlinNoise(123);
            foreach (var (x, y, z) in UnitSphereSamples())
            {
                double raw = noise.NoiseNormalized(x * 3, y * 3, z * 3);
                double fractal = noise.FractalNoiseNormalized(x * 3, y * 3, z * 3, 6);
                Assert.That(raw, Is.InRange(0.0, 1.0));
                Assert.That(fractal, Is.InRange(0.0, 1.0));
            }
        }

        [Test]
        public void ProducesSpatiallyVariedValues()
        {
            var noise = new PerlinNoise(55);
            var values = UnitSphereSamples()
                .Select(p => noise.FractalNoiseNormalized(p.X * 1.7, p.Y * 1.7, p.Z * 1.7, 5))
                .ToList();

            // Not a flat field: a real spread of values across the sphere.
            Assert.That(values.Max() - values.Min(), Is.GreaterThan(0.2),
                "3-D noise over the sphere should vary, not return a near-constant");
            Assert.That(values.Distinct().Count(), Is.GreaterThan(values.Count / 2));
        }

        [Test]
        public void IsSensitiveToSeed()
        {
            var a = new PerlinNoise(1);
            var b = new PerlinNoise(2);
            int differing = UnitSphereSamples().Count(p =>
                Math.Abs(a.Noise(p.X * 2, p.Y * 2, p.Z * 2) - b.Noise(p.X * 2, p.Y * 2, p.Z * 2)) > 1e-6);
            Assert.That(differing, Is.GreaterThan(0), "different seeds must give different fields");
        }

        [Test]
        public void IsContinuousBetweenNearbyPoints()
        {
            // Perlin noise is smooth: a tiny step in the domain yields a tiny change in value.
            var noise = new PerlinNoise(9);
            const double eps = 1e-3;
            foreach (var (x, y, z) in UnitSphereSamples(60))
            {
                double v0 = noise.Noise(x * 1.7, y * 1.7, z * 1.7);
                double v1 = noise.Noise((x + eps) * 1.7, y * 1.7, z * 1.7);
                Assert.That(Math.Abs(v1 - v0), Is.LessThan(0.05),
                    "a 1e-3 step must not jump the noise value");
            }
        }
    }
}
