using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using Aetherium.Core;

namespace Aetherium.Components
{
    public class Perception : Component
    {
        public ConcurrentQueue<PerceptionFrame> Perceptions { get; protected set; }

        public Perception() : base()
        {
            Perceptions = new ConcurrentQueue<PerceptionFrame>();
        }

        public void Sense(PerceptionFrame frame)
        {
            Perceptions.Enqueue(frame);
        }
    }
}

