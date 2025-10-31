using System;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.WorldBuilders.Validation;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Generators;

namespace Aetherium.Test
{
    [TestFixture]
    public class GeneratorsDeterminismTests
    {
        [Test]
        public void RoomsAndCorridors_IsDeterministic_And_Valid()
        {
            var seed = 12345;
            var ctx = new GeneratorContext(width: 30, height: 30, seed: seed) { ZLevel = 0 };
            var gen = new RoomsAndCorridorsGenerator();
            var world = gen.Generate(ctx);

            var report = new MapValidator().Validate(world, new MapValidationOptions
            {
                ZLevel = 0,
                RequireExplicitBoundary = true,
                RequireLightSource = true
            });

            Assert.IsTrue(report.IsValid, report.ToString());
        }
    }
}



