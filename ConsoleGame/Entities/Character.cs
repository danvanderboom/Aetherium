using System;
using System.Linq;
using System.Drawing;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame
{
    public class Character : Entity
    {
        public Character() : base()
        {
            Components.Add(new Location());
            Components.Add(new Health());
        }
    }
}