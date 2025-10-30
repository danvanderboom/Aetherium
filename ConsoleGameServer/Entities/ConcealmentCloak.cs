using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class ConcealmentCloak : Item
    {
        public ConcealmentCloak() : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Concealment Cloak";
            carriable.Icon = "H"; // H for hidden/concealment

            // When worn/equipped, affects perception and visibility
            Set(new Hidden
            {
                IsHidden = true,
                DiscoveryDifficulty = 0.7 // Makes wearer harder to detect
            });
        }
    }
}
