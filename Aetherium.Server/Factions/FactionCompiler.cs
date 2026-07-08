using System.Collections.Generic;
using Aetherium.Model.Factions;

namespace Aetherium.Server.Factions
{
    /// <summary>
    /// Compiles pure-data faction content (a world's <see cref="FactionConfig"/>) into the runtime
    /// tier the standing loop consumes: a <see cref="FactionRegistry"/> (each definition's doctrine
    /// deltas becoming a <see cref="FactionDoctrine"/>) and a <see cref="FactionRelations"/> matrix.
    /// The data→behavior seam that lets factions be per-world content rather than engine-hardcoded —
    /// mirrors <c>AbilityCompiler</c>/<c>ProgressionCompiler</c> and resolves add-factions' ownership
    /// question as "plain classes per map, config persisted" (see wire-factions-live design.md).
    /// </summary>
    public class FactionCompiler
    {
        public FactionRegistry CompileRegistry(IEnumerable<FactionDefinition>? definitions)
        {
            var registry = new FactionRegistry();
            if (definitions is null)
                return registry;

            foreach (var def in definitions)
            {
                var doctrine = new FactionDoctrine();
                foreach (var kv in def.DoctrineDeltas)
                    doctrine.SetDelta(kv.Key, kv.Value);

                registry.Add(new Faction(def.Id, def.Name, doctrine, def.Tags));
            }

            return registry;
        }

        public FactionRelations CompileRelations(IEnumerable<FactionRelationDefinition>? definitions)
        {
            var relations = new FactionRelations();
            if (definitions is null)
                return relations;

            foreach (var def in definitions)
            {
                var disposition = MapDisposition(def.Disposition);
                if (def.Mutual)
                    relations.SetMutual(def.FromFactionId, def.ToFactionId, disposition);
                else
                    relations.SetDisposition(def.FromFactionId, def.ToFactionId, disposition);
            }

            return relations;
        }

        private static FactionDisposition MapDisposition(FactionDispositionKind kind) => kind switch
        {
            FactionDispositionKind.War => FactionDisposition.War,
            FactionDispositionKind.Cold => FactionDisposition.Cold,
            FactionDispositionKind.Ally => FactionDisposition.Ally,
            FactionDispositionKind.Subordinate => FactionDisposition.Subordinate,
            _ => FactionDisposition.Neutral,
        };
    }
}
