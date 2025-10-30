using System;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame.Entities
{
    public class Snake : Character
    {
        Random rand;

        public Snake() : base()
        {
            rand = new Random();

            Set(new Perception());
        }
    }
}
