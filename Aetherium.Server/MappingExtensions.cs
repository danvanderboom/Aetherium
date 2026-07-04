using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Model;

namespace Aetherium.Server
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

        public static Aetherium.Model.WorldDirection ToDto(this Aetherium.WorldDirection direction)
        {
            return direction switch
            {
                Aetherium.WorldDirection.North => Aetherium.Model.WorldDirection.North,
                Aetherium.WorldDirection.South => Aetherium.Model.WorldDirection.South,
                Aetherium.WorldDirection.East => Aetherium.Model.WorldDirection.East,
                Aetherium.WorldDirection.West => Aetherium.Model.WorldDirection.West,
                Aetherium.WorldDirection.Up => Aetherium.Model.WorldDirection.Up,
                Aetherium.WorldDirection.Down => Aetherium.Model.WorldDirection.Down,
                _ => Aetherium.Model.WorldDirection.North
            };
        }

        public static Aetherium.WorldDirection ToEngineDirection(this Aetherium.Model.WorldDirection dto)
        {
            return dto switch
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

        public static Aetherium.RelativeDirection ToEngineRelativeDirection(this Aetherium.Model.RelativeDirection dto)
        {
            return dto switch
            {
                Aetherium.Model.RelativeDirection.Forward => Aetherium.RelativeDirection.Forward,
                Aetherium.Model.RelativeDirection.Backward => Aetherium.RelativeDirection.Backward,
                Aetherium.Model.RelativeDirection.Left => Aetherium.RelativeDirection.Left,
                Aetherium.Model.RelativeDirection.Right => Aetherium.RelativeDirection.Right,
                Aetherium.Model.RelativeDirection.Up => Aetherium.RelativeDirection.Up,
                Aetherium.Model.RelativeDirection.Down => Aetherium.RelativeDirection.Down,
                _ => Aetherium.RelativeDirection.Forward
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
                    kvp => (Aetherium.Model.VisualType)(int)kvp.Key,
                    kvp => kvp.Value.Count)
            };
        }

        public static ItemDto ToDto(this Aetherium.Core.Entity entity)
        {
            var itemDto = new ItemDto
            {
                Id = entity.EntityId,
            };

            var carriable = entity.AllComponents.OfType<Carriable>().FirstOrDefault();
            if (carriable != null)
            {
                itemDto.Label = carriable.Label;
                itemDto.Icon = carriable.Icon;
            }

            var key = entity.AllComponents.OfType<Aetherium.Components.Key>().FirstOrDefault();
            if (key != null)
                itemDto.KeyId = key.KeyId;

            return itemDto;
        }

        /// <summary>
        /// Projects a character entity (monster/NPC or another player) into the DTO
        /// the client renders. The glyph/color come from the entity's Tile component
        /// (e.g. the "Monster" tile — 'M', dark-red), mirroring how terrain carries
        /// its TileType to the wire.
        /// </summary>
        public static CharacterDto ToCharacterDto(this Aetherium.Character character)
        {
            var tileType = character.Get<Aetherium.Components.Tile>()?.Type;
            return new CharacterDto
            {
                Id = character.EntityId,
                Name = tileType?.Name ?? character.GetType().Name,
                Tile = tileType?.ToDto(),
                // Monsters (incl. Zombie via inheritance) and snakes are the hostile
                // NPCs today; player characters are plain Character instances.
                IsHostile = character is Aetherium.Monster || character is Aetherium.Entities.Snake,
            };
        }

        public static InventoryDto ToDto(this Inventory inventory)
        {
            var dto = new InventoryDto { Capacity = inventory.Capacity };
            foreach (var id in inventory.ItemEntityIds)
            {
                if (inventory.Items.TryGetValue(id, out var e))
                    dto.Items.Add(e.ToDto());
            }
            return dto;
        }
    }
}


