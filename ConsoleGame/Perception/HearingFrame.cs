using System.Collections.Generic;

namespace ConsoleGame.Components
{
    public class HearingFrame : PerceptionFrame
    {
        public List<Sound> Sounds { get; protected set; }

        public HearingFrame() : base()
        {
            Sounds = new List<Sound>();
        }
    }
}
