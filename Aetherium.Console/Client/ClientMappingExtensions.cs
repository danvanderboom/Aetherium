using System;
using Aetherium.Model;

namespace Aetherium.Client
{
    public static class ClientMappingExtensions
    {
        public static Aetherium.WorldDirection ToClientDirection(this Aetherium.Model.WorldDirection direction)
        {
            return direction switch
            {
                Aetherium.Model.WorldDirection.North => Aetherium.WorldDirection.North,
                Aetherium.Model.WorldDirection.South => Aetherium.WorldDirection.South,
                Aetherium.Model.WorldDirection.East => Aetherium.WorldDirection.East,
                Aetherium.Model.WorldDirection.West => Aetherium.WorldDirection.West,
                Aetherium.Model.WorldDirection.Up => Aetherium.WorldDirection.Up,
                Aetherium.Model.WorldDirection.Down => Aetherium.WorldDirection.Down,
                _ => Aetherium.WorldDirection.North
            };
        }
    }
}




