namespace ConsoleGame.Components
{
    public class Sound
    {
        public Location Location { get; set; }

        public SoundType Type { get; set; }

        public int Degrees { get; set; }

        public double Intensity { get; set; }

        public Sound() { }

        public Sound(Location location, SoundType type, int degrees, double intensity)
        {
            Location = location;
            Type = type;
            Degrees = degrees;
            Intensity = intensity;
        }
    }
}
