using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Entities
{
    public class Lever : Item
    {
        public Lever() : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Lever";
            carriable.Icon = "/";

            // Lever can be activated to control mechanisms
            Set(new Activatable
            {
                IsActivated = false,
                ToggleBehavior = true, // Can be toggled on/off
                TargetEntityIds = new System.Collections.Generic.List<string>()
            });
        }
    }
}


