using Aetherium.Core;

namespace Aetherium.Components
{
    public enum ClimbDirection
    {
        Up,
        Down,
        Both
    }

    public class Climbable : Component
    {
        public ClimbDirection Direction { get; set; } = ClimbDirection.Both;
        public bool RequiresItem { get; set; } = false;
        public string? RequiredItemId { get; set; } = null;

        public Climbable() : base() { }
    }
}


