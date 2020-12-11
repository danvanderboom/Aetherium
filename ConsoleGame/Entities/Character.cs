using System.Drawing;
using ConsoleGame.Components;
using ConsoleGame.Core;

namespace ConsoleGame
{
    public class Character : Entity
    {
        public string Name { get; set; }

        public int MaxHealth { get; set; }

        public int Health { get; set; }

        public Position Location { get; set; }

        public Direction PreviousDirection { get; set; }

        public Character() : base()
        {
            Components.Add(new Position());
            Components.Add(new Health());

            var mind = new Mind();
            Components.Add(new Mind());

            var health = new Health();
            mind.Components.Add(health);
        }
    }
}