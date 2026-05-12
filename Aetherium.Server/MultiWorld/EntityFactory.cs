using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Materializes <see cref="Entity"/> instances from <see cref="EntityPlacement"/>
    /// records during snapshot hydration. Reflection-first: any concrete
    /// <see cref="Entity"/> subclass with a parameterless constructor is creatable
    /// out of the box. Special cases (constructors that need a <c>World</c> reference
    /// or required arguments) are registered explicitly.
    ///
    /// <para>
    /// Snapshot side (capture) is symmetric: <see cref="ExtractProperties"/> reads
    /// the same component state that <see cref="ApplyProperties"/> writes back.
    /// </para>
    /// </summary>
    public sealed class EntityFactory
    {
        private readonly World _world;

        // Cache type lookups so each placement doesn't re-scan reflection metadata.
        private static readonly ConcurrentDictionary<string, Type?> TypeCache = new();

        public EntityFactory(World world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>
        /// Instantiates an <see cref="Entity"/> for the given placement, overrides its
        /// <see cref="Entity.EntityId"/> to the snapshot-supplied value, attaches the
        /// location, and re-applies any captured properties. Returns null if the type
        /// can't be resolved or constructed — caller decides whether to log/skip.
        /// </summary>
        public Entity? Create(EntityPlacement placement)
        {
            if (placement is null || string.IsNullOrEmpty(placement.TypeName))
                return null;

            var type = ResolveType(placement.TypeName);
            if (type is null)
                return null;

            var entity = Construct(type, placement);
            if (entity is null)
                return null;

            // Authoritative ID is the grain-assigned one in the snapshot, not whatever
            // the constructor produced from Guid.NewGuid.
            entity.EntityId = placement.EntityId;
            entity.Set(placement.ToWorldLocation());

            ApplyProperties(entity, placement);

            return entity;
        }

        /// <summary>
        /// Captures the entity-specific properties that hydration needs to re-apply.
        /// Counterpart of <see cref="ApplyProperties"/>.
        /// </summary>
        public static void ExtractProperties(Entity entity, EntityPlacement placement)
        {
            // Door open/closed state
            if (entity.Components.TryGetValue(typeof(OpensAndCloses), out var ocComp)
                && ocComp is OpensAndCloses opens)
            {
                placement.Properties["IsOpen"] = opens.IsOpen ? "true" : "false";
                placement.Properties["IsLocked"] = opens.IsLocked ? "true" : "false";
                if (!string.IsNullOrEmpty(opens.KeyShape))
                    placement.Properties["KeyShape"] = opens.KeyShape;
            }

            // Key carries its KeyId
            if (entity.Components.TryGetValue(typeof(Key), out var keyComp)
                && keyComp is Key key)
            {
                placement.Properties["KeyId"] = key.KeyId;
            }

            // Portals carry their routing info
            if (entity.Components.TryGetValue(typeof(PortalComponent), out var portalComp)
                && portalComp is PortalComponent portal)
            {
                placement.Properties["PortalId"] = portal.PortalId ?? string.Empty;
                if (!string.IsNullOrEmpty(portal.TargetWorldId))
                    placement.Properties["TargetWorldId"] = portal.TargetWorldId!;
                if (!string.IsNullOrEmpty(portal.TargetMapId))
                    placement.Properties["TargetMapId"] = portal.TargetMapId!;
                if (!string.IsNullOrEmpty(portal.TargetTag))
                    placement.Properties["TargetTag"] = portal.TargetTag!;
                if (!string.IsNullOrEmpty(portal.Activation))
                    placement.Properties["Activation"] = portal.Activation!;
            }
        }

        private static void ApplyProperties(Entity entity, EntityPlacement placement)
        {
            if (placement.Properties.Count == 0)
                return;

            if (entity.Components.TryGetValue(typeof(OpensAndCloses), out var ocComp)
                && ocComp is OpensAndCloses opens)
            {
                if (placement.Properties.TryGetValue("IsOpen", out var isOpen))
                    opens.IsOpen = string.Equals(isOpen, "true", StringComparison.OrdinalIgnoreCase);
                if (placement.Properties.TryGetValue("IsLocked", out var isLocked))
                    opens.IsLocked = string.Equals(isLocked, "true", StringComparison.OrdinalIgnoreCase);
                if (placement.Properties.TryGetValue("KeyShape", out var keyShape))
                    opens.KeyShape = keyShape;
            }

            if (entity.Components.TryGetValue(typeof(PortalComponent), out var portalComp)
                && portalComp is PortalComponent portal)
            {
                if (placement.Properties.TryGetValue("PortalId", out var portalId))
                    portal.PortalId = portalId;
                if (placement.Properties.TryGetValue("TargetWorldId", out var tw))
                    portal.TargetWorldId = tw;
                if (placement.Properties.TryGetValue("TargetMapId", out var tm))
                    portal.TargetMapId = tm;
                if (placement.Properties.TryGetValue("TargetTag", out var tt))
                    portal.TargetTag = tt;
                if (placement.Properties.TryGetValue("Activation", out var act))
                    portal.Activation = act;
            }
        }

        private Entity? Construct(Type type, EntityPlacement placement)
        {
            // Special-case types that don't have a parameterless constructor or that
            // need engine wiring (e.g. a World reference for AI heartbeat).
            if (type == typeof(KeyItem))
            {
                placement.Properties.TryGetValue("KeyId", out var keyId);
                return new KeyItem(keyId ?? string.Empty);
            }

            if (type == typeof(Monster))
                return new Monster(_world);

            if (type == typeof(Zombie))
                return new Zombie(_world);

            // Default: try a parameterless ctor; if none, try the first public ctor
            // whose parameters all have default values (common pattern in this codebase,
            // e.g. `public FoodItem(int uses = 5) : base()`). Activator.CreateInstance
            // does not auto-fill default values, so we do it explicitly.
            try
            {
                var ctorNoArgs = type.GetConstructor(Type.EmptyTypes);
                if (ctorNoArgs is not null)
                    return ctorNoArgs.Invoke(null) as Entity;

                var defaultableCtor = type.GetConstructors()
                    .FirstOrDefault(c => c.GetParameters().All(p => p.HasDefaultValue));
                if (defaultableCtor is not null)
                {
                    var args = defaultableCtor.GetParameters()
                        .Select(p => p.DefaultValue)
                        .ToArray();
                    return defaultableCtor.Invoke(args) as Entity;
                }

                return null;
            }
            catch (Exception)
            {
                // Construction can throw for engine reasons (missing tile-type
                // registrations, etc.) — caller decides whether to log/skip.
                return null;
            }
        }

        private static Type? ResolveType(string typeName)
        {
            return TypeCache.GetOrAdd(typeName, name =>
            {
                // Search the assemblies that host Entity subclasses. In phase 1 that's
                // Aetherium.Server (where the gameplay Entity types live) plus the
                // shared Aetherium.Model assembly for any DTO-side entity types.
                var candidates = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name?.StartsWith("Aetherium", StringComparison.Ordinal) == true)
                    .SelectMany(SafeGetTypes)
                    .Where(t => !t.IsAbstract && typeof(Entity).IsAssignableFrom(t) && t.Name == name)
                    .ToList();

                // Prefer exact match; if multiple assemblies define the same short name,
                // prefer the Server one (gameplay types) over Console/Model.
                return candidates.FirstOrDefault(t => t.Assembly.GetName().Name == "Aetherium.Server")
                       ?? candidates.FirstOrDefault();
            });
        }

        private static Type[] SafeGetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).ToArray()!; }
        }
    }
}
