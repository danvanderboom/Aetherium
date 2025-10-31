using System;
using System.Linq;
using System.Collections.Generic;
using Aetherium.Core;
using Aetherium.Components;
using Aetherium.Entities;

namespace Aetherium.WorldBuilders
{
	public class AudioTestWorldBuilder : WorldBuilder
	{
		public WorldLocation StartLocation { get; } = new WorldLocation(10, 10, 0);

		public AudioTestWorldBuilder() : base() { }

		public override World Build()
		{
			var world = new World();

			world.AddTileTypes(TileTypes);
			world.AddTerrainTypes(CreateTerrainTypes(TileTypes));

			BuildTestArea(world);
			return world;
		}

		private void BuildTestArea(World world)
		{
			const int size = 22;
			const int z = 0;

			// Fill with walls
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
					world.SetTerrain("Wall", new WorldLocation(x, y, z));
			}

			// Central room 9x9 around StartLocation
			for (int y = StartLocation.Y - 4; y <= StartLocation.Y + 4; y++)
			{
				for (int x = StartLocation.X - 4; x <= StartLocation.X + 4; x++)
					world.SetTerrain("Indoors", new WorldLocation(x, y, z));
			}

			// Corridors to test footsteps
			for (int x = 2; x < size - 2; x++)
				world.SetTerrain("Indoors", new WorldLocation(x, StartLocation.Y, z));
			for (int y = 2; y < size - 2; y++)
				world.SetTerrain("Indoors", new WorldLocation(StartLocation.X, y, z));

			// Unlocked door on the east side of the room
			var doorA = new Door();
			doorA.Set(new WorldLocation(StartLocation.X + 5, StartLocation.Y, z));
			world.AddEntity(doorA);

			// Locked door on the west side of the room
			var doorB = new Door();
			doorB.Set(new WorldLocation(StartLocation.X - 5, StartLocation.Y, z));
			var oc = doorB.Get<OpensAndCloses>();
			if (oc != null)
			{
				oc.IsLocked = true;
				oc.IsOpen = false;
			}
			world.AddEntity(doorB);

			// Lever (activatable) north of start to unlock DoorB and open it
			var lever = new Item();
			lever.Set(new WorldLocation(StartLocation.X, StartLocation.Y - 3, z));
			var activatable = new Activatable { ToggleBehavior = true };
			activatable.TargetEntityIds.Add(doorB.EntityId);
			lever.Set(activatable);
			world.AddEntity(lever);

			// Place 2 simple items for pickup/drop
			var item1 = new Item();
			item1.Set(new WorldLocation(StartLocation.X + 1, StartLocation.Y, z));
			var c1 = item1.Get<Carriable>();
			if (c1 != null) { c1.Label = "Gem"; c1.Icon = "*"; }
			world.AddEntity(item1);

			var item2 = new Item();
			item2.Set(new WorldLocation(StartLocation.X, StartLocation.Y + 1, z));
			var c2 = item2.Get<Carriable>();
			if (c2 != null) { c2.Label = "Coin"; c2.Icon = "$"; }
			world.AddEntity(item2);
		}

		string[] TerrainTypeNames => new string[]
		{
			"None",
			"Indoors",
			"Wall",
			"Mountain",
			"Road",
			"Plains",
			"Forest",
			"Water",
			"Cave",
			"Upstairs",
			"Downstairs"
		};

		public List<TerrainType> CreateTerrainTypes(IList<TileType> tileTypes) =>
			TileTypes
			.Select(t => new TerrainType
			{
				Name = t.Name,
				TileType = tileTypes.First(tt => tt.Name == t.Name),
				Settings = t.Settings
			})
			.Where(t => TerrainTypeNames.Contains(t.Name))
			.ToList();

		public List<TileType> TileTypes => new List<TileType>
		{
			new TileType
			{
				Name = "None",
				DefaultComponents = new List<Component> { new ObstructsMovement(), new ObstructsView() },
				Settings = new Dictionary<string, string>
				{
					{ "MapCharacter", " " },
					{ "BackgroundColor", ConsoleColor.Black.ToString() },
					{ "ForegroundColor", ConsoleColor.Black.ToString() },
				}
			},
			new TileType
			{
				Name = "Indoors",
				Settings = new Dictionary<string, string>
				{
					{ "MapCharacter", " " },
					{ "BackgroundColor", ConsoleColor.Gray.ToString() },
					{ "ForegroundColor", ConsoleColor.Black.ToString() },
				}
			},
			new TileType
			{
				Name = "Wall",
				DefaultComponents = new List<Component> { new ObstructsView { Opacity = 1 } },
				Settings = new Dictionary<string, string>
				{
					{ "MapCharacter", "|" },
					{ "BackgroundColor", ConsoleColor.Gray.ToString() },
					{ "ForegroundColor", ConsoleColor.DarkRed.ToString() },
				}
			},
			new TileType
			{
				Name = "Mountain",
				DefaultComponents = new List<Component> { new ObstructsView { Opacity = 1 } },
				Settings = new Dictionary<string, string>
				{
					{ "MapCharacter", "^" },
					{ "BackgroundColor", ConsoleColor.DarkGray.ToString() },
					{ "ForegroundColor", ConsoleColor.White.ToString() },
				}
			},
			new TileType
			{
				Name = "Road",
				Settings = new Dictionary<string, string>
				{
					{ "MapCharacter", "=" },
					{ "BackgroundColor", ConsoleColor.Black.ToString() },
					{ "ForegroundColor", ConsoleColor.White.ToString() },
				}
			},
			new TileType
			{
				Name = "Plains",
				Settings = new Dictionary<string, string>
				{
					{ "MapCharacter", "." },
					{ "BackgroundColor", ConsoleColor.DarkYellow.ToString() },
					{ "ForegroundColor", ConsoleColor.Yellow.ToString() },
				}
			},
			new TileType
			{
				Name = "Forest",
				DefaultComponents = new List<Component> { new ObstructsView { Opacity = 0.49 } },
				Settings = new Dictionary<string, string>
				{
					{ "MapCharacter", "t" },
					{ "BackgroundColor", ConsoleColor.Black.ToString() },
					{ "ForegroundColor", ConsoleColor.Green.ToString() },
				}
			},
			new TileType
			{
				Name = "Water",
				DefaultComponents = new List<Component> { new ObstructsMovement() },
				Settings = new Dictionary<string, string>
				{
					{ "MapCharacter", "~" },
					{ "BackgroundColor", ConsoleColor.Blue.ToString() },
					{ "ForegroundColor", ConsoleColor.White.ToString() },
				}
			},
			new TileType
			{
				Name = "Cave",
				Settings = new Dictionary<string, string>
				{
					{ "MapCharacter", "t" },
					{ "BackgroundColor", ConsoleColor.Black.ToString() },
					{ "ForegroundColor", ConsoleColor.DarkGray.ToString() },
				}
			},
			new TileType
			{
				Name = "Upstairs",
				Settings = new Dictionary<string, string>
				{
					{ "MapCharacter", "+" },
					{ "BackgroundColor", ConsoleColor.Gray.ToString() },
					{ "ForegroundColor", ConsoleColor.Yellow.ToString() },
				}
			},
			new TileType
			{
				Name = "Downstairs",
				Settings = new Dictionary<string, string>
				{
					{ "MapCharacter", "-" },
					{ "BackgroundColor", ConsoleColor.Gray.ToString() },
					{ "ForegroundColor", ConsoleColor.Yellow.ToString() },
				}
			},
			new TileType
			{
				Name = "Player",
				Settings = new Dictionary<string, string>
				{
					{ "MapCharacter", "*" },
					{ "BackgroundColor", ConsoleColor.White.ToString() },
					{ "ForegroundColor", ConsoleColor.Blue.ToString() },
				}
			}
		};
	}
}

