using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class Health : Component
    {
        public int Level { get; set; }

        public int MaxLevel { get; set; }

        public Health() { }

        public Health(int level, int maxLevel) 
        {
            Level = level;
            MaxLevel = maxLevel;
        }
    }
}