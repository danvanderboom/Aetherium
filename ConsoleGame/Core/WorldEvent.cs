using ConsoleGame.Components;

namespace ConsoleGame.Core
{
    public class WorldEvent
    {
        public WorldEventType EventType { get; set; }

        public WorldLocation Location { get; set; } = WorldLocation.None;

        public Entity? Entity { get; set; }
    }
}
