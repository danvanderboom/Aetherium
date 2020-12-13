using System;
using System.Text;
using System.Collections.Generic;
using ConsoleGame.Core;

namespace ConsoleGame.Components
{
    public class OpensAndCloses : Component
    {
        public bool IsOpen { get; set; }

        public bool IsLocked { get; set; }

        public string KeyShape { get; set; }

        public OpensAndCloses() : base() { }
    }
}
