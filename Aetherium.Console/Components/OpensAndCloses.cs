using System;
using System.Text;
using System.Collections.Generic;
using Aetherium.Core;

namespace Aetherium.Components
{
    public class OpensAndCloses : Component
    {
        public bool IsOpen { get; set; } = false;

        public bool IsLocked { get; set; } = false;

        public string KeyShape { get; set; } = string.Empty;

        public OpensAndCloses() : base() { }
    }
}

