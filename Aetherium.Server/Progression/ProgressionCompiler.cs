using System.Collections.Generic;
using Aetherium.Model.Progression;

namespace Aetherium.Server.Progression
{
    /// <summary>
    /// Compiles pure-data progression content (a world's <see cref="ProgressionConfig"/>) into the
    /// runtime tier the live loop consumes: a <see cref="SkillCatalog"/>, per-pool
    /// <see cref="ILevelCurve"/> instances, and fresh per-character
    /// <see cref="ProgressPools"/>/<see cref="Attributes"/>/<see cref="RoleAffinity"/> components.
    /// The data→behavior seam that lets progression be per-world content rather than engine-hardcoded
    /// — mirrors <c>AbilityCompiler</c> and the ContentAtlas data(Model)/seeding(Server) split.
    /// </summary>
    public class ProgressionCompiler
    {
        public SkillCatalog CompileSkillCatalog(IEnumerable<SkillDefinitionData>? skills)
        {
            var catalog = new SkillCatalog();
            if (skills is null)
                return catalog;

            foreach (var s in skills)
            {
                catalog.Add(new SkillDefinition(
                    s.Id,
                    s.Description,
                    prerequisites: s.Prerequisites,
                    unlocksAbilityId: s.UnlocksAbilityId,
                    modifiesAttributeId: s.ModifiesAttributeId,
                    modifierAmount: s.ModifierAmount,
                    requiredPoolId: s.RequiredPoolId,
                    requiredLevel: s.RequiredLevel));
            }

            return catalog;
        }

        /// <summary>The level curve for each defined pool, keyed by pool id — used by the XP-award
        /// path to recompute a pool's level after crediting XP.</summary>
        public IReadOnlyDictionary<string, ILevelCurve> CompileCurvesByPool(IEnumerable<ProgressPoolDefinition>? pools)
        {
            var curves = new Dictionary<string, ILevelCurve>();
            if (pools is null)
                return curves;

            foreach (var p in pools)
                curves[p.Id] = BuildCurve(p.Curve);

            return curves;
        }

        /// <summary>Builds a fresh <see cref="ProgressPools"/> component from definitions (each pool
        /// seeded with its configured starting xp/level) — called once per joining character.</summary>
        public ProgressPools BuildProgressPools(IEnumerable<ProgressPoolDefinition>? pools)
        {
            var result = new ProgressPools();
            if (pools is null)
                return result;

            foreach (var p in pools)
                result.Add(new ProgressPool(p.Id, p.StartingXp, p.StartingLevel));

            return result;
        }

        public Attributes BuildAttributes(IReadOnlyDictionary<string, double>? starting)
        {
            var attrs = new Attributes();
            if (starting is not null)
                foreach (var kv in starting)
                    attrs.Set(kv.Key, kv.Value);
            return attrs;
        }

        public RoleAffinity BuildRoleAffinity(IReadOnlyDictionary<string, double>? starting)
        {
            var affinity = new RoleAffinity();
            if (starting is not null)
                foreach (var kv in starting)
                    affinity.Set(kv.Key, kv.Value);
            return affinity;
        }

        public ILevelCurve BuildCurve(LevelCurveDefinition def) => def.Kind switch
        {
            LevelCurveKind.Linear => new LinearLevelCurve(def.XpPerLevel <= 0 ? 1 : def.XpPerLevel),
            _ => new LinearLevelCurve(def.XpPerLevel <= 0 ? 1 : def.XpPerLevel),
        };
    }
}
