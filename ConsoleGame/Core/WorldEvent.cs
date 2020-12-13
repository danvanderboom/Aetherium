using ConsoleGame.Components;

namespace ConsoleGame.Core
{
    public class WorldEvent
    {
        public WorldEventType EventType { get; set; }

        public Location Location { get; set; }

        public Entity Entity { get; set; }
    }
}
