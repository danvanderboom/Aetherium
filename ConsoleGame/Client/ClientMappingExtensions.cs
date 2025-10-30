using System;
using ConsoleGameModel;

namespace ConsoleGame.Client
{
    public static class ClientMappingExtensions
    {
        public static ConsoleGame.WorldDirection ToClientDirection(this ConsoleGameModel.WorldDirection direction)
        {
            return direction switch
            {
                ConsoleGameModel.WorldDirection.North => ConsoleGame.WorldDirection.North,
                ConsoleGameModel.WorldDirection.South => ConsoleGame.WorldDirection.South,
                ConsoleGameModel.WorldDirection.East => ConsoleGame.WorldDirection.East,
                ConsoleGameModel.WorldDirection.West => ConsoleGame.WorldDirection.West,
                ConsoleGameModel.WorldDirection.Up => ConsoleGame.WorldDirection.Up,
                ConsoleGameModel.WorldDirection.Down => ConsoleGame.WorldDirection.Down,
                _ => ConsoleGame.WorldDirection.North
            };
        }
    }
}

