using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Server.Progression;
using Aetherium.Model.Progression;

namespace Aetherium.Test.Progression
{
    /// <summary>
    /// Verifies "Per-World Progression Config" (openspec/changes/wire-progression-live): the
    /// <see cref="ProgressionCompiler"/> turns pure-data <see cref="ProgressionConfig"/> content into
    /// the runtime tier (skill catalog, per-pool curves, fresh per-character components).
    /// </summary>
    [TestFixture]
    public class ProgressionCompilerTests
    {
        [Test]
        public void CompilePools_ProducesWorkingPoolsAndCurves()
        {
            var defs = new List<ProgressPoolDefinition>
            {
                new() { Id = "combat", Curve = new LevelCurveDefinition { Kind = LevelCurveKind.Linear, XpPerLevel = 100 }, StartingXp = 0, StartingLevel = 1 },
            };
            var compiler = new ProgressionCompiler();

            var pools = compiler.BuildProgressPools(defs);
            Assert.That(pools.Pools.ContainsKey("combat"), Is.True);
            Assert.That(pools.Pools["combat"].Level, Is.EqualTo(1));

            var curves = compiler.CompileCurvesByPool(defs);
            Assert.That(curves.ContainsKey("combat"), Is.True);

            // The compiled curve behaves like the linear default: 250 xp / 100-per-level → level 3.
            pools.AddXp("combat", 250, curves["combat"]);
            Assert.That(pools.Pools["combat"].Level, Is.EqualTo(3));
        }

        [Test]
        public void BuildProgressPools_HonorsStartingXpAndLevel()
        {
            var defs = new List<ProgressPoolDefinition>
            {
                new() { Id = "exploration", Curve = new LevelCurveDefinition { XpPerLevel = 50 }, StartingXp = 120, StartingLevel = 4 },
            };

            var pools = new ProgressionCompiler().BuildProgressPools(defs);

            Assert.That(pools.Pools["exploration"].Xp, Is.EqualTo(120));
            Assert.That(pools.Pools["exploration"].Level, Is.EqualTo(4));
        }

        [Test]
        public void CompileSkillCatalog_ProducesCatalog_WithGateAndEffectFields()
        {
            var defs = new List<SkillDefinitionData>
            {
                new()
                {
                    Id = "fireball_training", Description = "Learn fireball.",
                    Prerequisites = new List<string> { "arcane_basics" },
                    UnlocksAbilityId = "fireball", ModifiesAttributeId = "intellect", ModifierAmount = 5,
                    RequiredPoolId = "arcane", RequiredLevel = 2,
                },
            };

            var catalog = new ProgressionCompiler().CompileSkillCatalog(defs);

            Assert.That(catalog.TryGet("fireball_training", out var skill), Is.True);
            Assert.That(skill!.UnlocksAbilityId, Is.EqualTo("fireball"));
            Assert.That(skill.ModifiesAttributeId, Is.EqualTo("intellect"));
            Assert.That(skill.ModifierAmount, Is.EqualTo(5));
            Assert.That(skill.RequiredPoolId, Is.EqualTo("arcane"));
            Assert.That(skill.RequiredLevel, Is.EqualTo(2));
            Assert.That(skill.Prerequisites, Does.Contain("arcane_basics"));
        }

        [Test]
        public void BuildComponents_FromStartingDicts()
        {
            var compiler = new ProgressionCompiler();

            var attrs = compiler.BuildAttributes(new Dictionary<string, double> { ["vitality"] = 150, ["strength"] = 12 });
            Assert.That(attrs.Get("vitality"), Is.EqualTo(150));
            Assert.That(attrs.Get("strength"), Is.EqualTo(12));

            var affinity = compiler.BuildRoleAffinity(new Dictionary<string, double> { ["warrior"] = 1.0 });
            Assert.That(affinity.Get("warrior"), Is.EqualTo(1.0));
        }

        [Test]
        public void BuildProgressPools_ReturnsFreshInstances_NoSharedState()
        {
            var defs = new List<ProgressPoolDefinition>
            {
                new() { Id = "combat", Curve = new LevelCurveDefinition { XpPerLevel = 100 }, StartingXp = 50 },
            };
            var compiler = new ProgressionCompiler();

            var a = compiler.BuildProgressPools(defs);
            var b = compiler.BuildProgressPools(defs);
            a.AddXp("combat", 500, new LinearLevelCurve(100));

            Assert.That(b.Pools["combat"].Xp, Is.EqualTo(50), "Each character must get its own pool instances.");
        }

        [Test]
        public void CompileSkillCatalog_Null_ProducesEmptyCatalog()
        {
            var catalog = new ProgressionCompiler().CompileSkillCatalog(null);
            Assert.That(catalog.TryGet("anything", out _), Is.False);
        }
    }
}
