using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame.Entities
{
    public class SecretDoor : Door
    {
        public SecretDoor() : base()
        {
            // Secret doors are hidden by default
            Set(new Hidden
            {
                IsHidden = true,
                DiscoveryDifficulty = 0.6 // Moderate difficulty to discover
            });
        }
    }
}

