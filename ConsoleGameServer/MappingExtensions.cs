using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGameModel;

namespace ConsoleGameServer
{
    public static class MappingExtensions
    {
        public static WorldLocationDto ToDto(this WorldLocation location)
        {
            return new WorldLocationDto(location.X, location.Y, location.Z);
        }

        public static WorldLocation ToWorldLocation(this WorldLocationDto dto)
        {
            return new WorldLocation(dto.X, dto.Y, dto.Z);
        }

        public static ConsoleGameModel.WorldDirection ToDto(this ConsoleGame.WorldDirection direction)
        {
            return direction switch
            {
                ConsoleGame.WorldDirection.North => ConsoleGameModel.WorldDirection.North,
                ConsoleGame.WorldDirection.South => ConsoleGameModel.WorldDirection.South,
                ConsoleGame.WorldDirection.East => ConsoleGameModel.WorldDirection.East,
                ConsoleGame.WorldDirection.West => ConsoleGameModel.WorldDirection.West,
                ConsoleGame.WorldDirection.Up => ConsoleGameModel.WorldDirection.Up,
                ConsoleGame.WorldDirection.Down => ConsoleGameModel.WorldDirection.Down,
                _ => ConsoleGameModel.WorldDirection.North
            };
        }

        public static ConsoleGame.WorldDirection ToEngineDirection(this ConsoleGameModel.WorldDirection dto)
        {
            return dto switch
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

        public static ConsoleGame.RelativeDirection ToEngineRelativeDirection(this ConsoleGameModel.RelativeDirection dto)
        {
            return dto switch
            {
                ConsoleGameModel.RelativeDirection.Forward => ConsoleGame.RelativeDirection.Forward,
                ConsoleGameModel.RelativeDirection.Backward => ConsoleGame.RelativeDirection.Backward,
                ConsoleGameModel.RelativeDirection.Left => ConsoleGame.RelativeDirection.Left,
                ConsoleGameModel.RelativeDirection.Right => ConsoleGame.RelativeDirection.Right,
                ConsoleGameModel.RelativeDirection.Up => ConsoleGame.RelativeDirection.Up,
                ConsoleGameModel.RelativeDirection.Down => ConsoleGame.RelativeDirection.Down,
                _ => ConsoleGame.RelativeDirection.Forward
            };
        }

        public static TileTypeDto ToDto(this TileType tileType)
        {
            if (tileType == null)
                return new TileTypeDto();

            return new TileTypeDto(tileType.Name, new Dictionary<string, string>(tileType.Settings));
        }

        public static RectangleDto ToDto(this Rectangle rect)
        {
            return new RectangleDto(rect.X, rect.Y, rect.Width, rect.Height);
        }

        public static VisualDto ToDto(this Visual visual, double lightLevel = 1.0)
        {
            return new VisualDto
            {
                Location = visual.Location.ToDto(),
                Terrain = visual.Terrain?.ToDto(),
                LightLevel = lightLevel,
                ThingsSeen = visual.ThingsSeen.ToDictionary(
                    kvp => (ConsoleGameModel.VisualType)(int)kvp.Key,
                    kvp => kvp.Value.Count)
            };
        }
    }
}

