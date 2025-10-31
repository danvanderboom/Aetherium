using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Entities
{
    /// <summary>
    /// Entity representing a portal that enables travel between worlds/maps.
    /// </summary>
    public class PortalEntity : Entity
    {
        public PortalEntity() : base()
        {
            Set(new Carriable
            {
                Label = "Portal",
                Icon = "O"
            });
        }

        public PortalEntity(string portalId, string? targetWorldId = null, string? targetMapId = null, string? targetTag = null, string? activation = null)
            : this()
        {
            Set(new PortalComponent
            {
                PortalId = portalId,
                TargetWorldId = targetWorldId,
                TargetMapId = targetMapId,
                TargetTag = targetTag,
                Activation = activation,
                DisplayName = targetTag ?? "Portal",
                IsActive = true
            });
        }
    }
}

