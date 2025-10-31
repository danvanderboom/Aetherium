using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class Lockpick : Component
    {
        public int SkillLevel { get; set; } = 1; // 1-10, affects success chance
        public int Durability { get; set; } = 10;

        public Lockpick() : base() { }
    }
}

