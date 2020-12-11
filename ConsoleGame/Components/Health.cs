using ConsoleGame.Core;

namespace ConsoleGame
{
    public class Health : Component
    {
        public int Level { get; set; }

        public Health() { }

        public Health(int level) 
        {
            Level = level; 
        }
    }
}