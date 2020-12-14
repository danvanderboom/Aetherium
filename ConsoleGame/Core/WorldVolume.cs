using ConsoleGame.Components;

namespace ConsoleGame.Core
{
    public struct WorldVolume
    {
        public WorldLocation Location { get; set; }

        public Size3d Size { get; set; }

        public WorldVolume(WorldLocation location, Size3d size) 
        {
            Location = location;
            Size = size;
        }
    }
}
