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
            Components.TryAdd(typeof(Location), new Location());
            Components.TryAdd(typeof(Health), new Health());
        }
    }
}