using System.Collections.Generic;

namespace Aetherium.Components
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

