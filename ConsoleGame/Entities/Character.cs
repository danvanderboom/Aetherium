using System;
using System.Linq;
using System.Drawing;
using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame
{
    public class Character : Entity
    {
        public Character() : base()
        {
            Set(new Health());
            Set(new HasHeading());
            Set(new Perception());
            Set(new Memory());
        }
    }
}