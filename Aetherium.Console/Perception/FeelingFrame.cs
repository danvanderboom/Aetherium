using System;
using System.Linq;
using System.Collections.Generic;

namespace Aetherium.Components
{
    public class FeelingFrame : PerceptionFrame
    {
        public Dictionary<FeelingType, double> Feelings { get; protected set; }

        public FeelingFrame() : base()
        {
            Feelings = new Dictionary<FeelingType, double>();
        }
    }
}
