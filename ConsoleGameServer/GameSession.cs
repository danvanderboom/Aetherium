using System;
using System.Drawing;
using ConsoleGame;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGame.Entities;
using ConsoleGame.WorldBuilders;

namespace ConsoleGameServer
{
	public class GameSession
	{
		public string SessionId { get; set; } = Guid.NewGuid().ToString();
		public string ConnectionId { get; set; } = string.Empty;
		public World World { get; set; }
		public Character? Player { get; set; }
		public WorldLocation? ViewLocation { get; set; }
		public WorldDirection Heading { get; set; } = WorldDirection.North;
		public Size ViewportSize { get; set; } = new Size(42, 22); // Default console size

		private readonly PerceptionService perceptionService;
		private readonly Random rand = new Random();

		public GameSession(string connectionId, WorldBuilder worldBuilder)
		{
			ConnectionId = connectionId;
			perceptionService = new PerceptionService();

			// Build the world
			World = worldBuilder.Build();

			// Determine start location
			WorldLocation? startLocation = null;
			if (worldBuilder is AudioTestWorldBuilder audioBuilder)
			{
				startLocation = audioBuilder.StartLocation;
			}
			else
			{
				startLocation = World.SelectRandomPassableLocation();
			}

			// Initialize view location and player
			if (startLocation != null)
			{
				ViewLocation = startLocation;
				Player = new Character();
				Player.Set(new WorldLocation(startLocation.X, startLocation.Y, startLocation.Z));
				Player.Set(new Inventory());

				// For audio test, preload a compass into inventory so compass widget is visible
				if (worldBuilder is AudioTestWorldBuilder)
				{
					var compass = new CompassItem();
					var inv = Player.Get<Inventory>();
					inv?.TryAdd(compass.EntityId, compass);
				}

				World.AddEntity(Player);
			}
			else
			{
				// Fallback
				var locations = World.EntitiesByLocation.Keys;
				if (locations.Count > 0)
				{
					ViewLocation = new WorldLocation(15, 15, 0);
					Player = new Character();
					Player.Set(new WorldLocation(15, 15, 0));
					Player.Set(new Inventory());
					World.AddEntity(Player);
				}
			}
		}

		public ConsoleGameModel.PerceptionDto GetPerception()
		{
			if (ViewLocation == null)
				throw new InvalidOperationException("ViewLocation is null");

			return perceptionService.ComputePerception(World, ViewLocation, Heading, ViewportSize);
		}

		public void MoveView(ConsoleGameModel.RelativeDirection direction, int distance = 1)
		{
			if (ViewLocation == null)
				return;

			var engineDirection = direction.ToEngineRelativeDirection();

			var rightAngleRotationsCounterclockwise = engineDirection switch
			{
				RelativeDirection.Forward => 0,
				RelativeDirection.Left => 1,
				RelativeDirection.Backward => 2,
				RelativeDirection.Right => 3,
				_ => 0
			};

			var bearing = Heading;
			for (int i = 0; i < rightAngleRotationsCounterclockwise; i++)
				bearing = bearing.RotateLeft();

			ViewLocation = bearing switch
			{
				WorldDirection.North => ViewLocation.FromDelta(0, -distance, 0),
				WorldDirection.East => ViewLocation.FromDelta(distance, 0, 0),
				WorldDirection.South => ViewLocation.FromDelta(0, distance, 0),
				WorldDirection.West => ViewLocation.FromDelta(-distance, 0, 0),
				_ => ViewLocation
			};

			if (Player != null)
			{
				// Keep player entity co-located with the view
				var destination = new WorldLocation(ViewLocation.X, ViewLocation.Y, ViewLocation.Z);
				World.MoveEntity(Player.EntityId, destination);
			}
		}

		public void RotateView(bool clockwise)
		{
			if (clockwise)
			{
				Heading = Heading switch
				{
					WorldDirection.North => WorldDirection.East,
					WorldDirection.East => WorldDirection.South,
					WorldDirection.South => WorldDirection.West,
					WorldDirection.West => WorldDirection.North,
					_ => Heading
				};
			}
			else
			{
				Heading = Heading switch
				{
					WorldDirection.North => WorldDirection.West,
					WorldDirection.West => WorldDirection.South,
					WorldDirection.South => WorldDirection.East,
					WorldDirection.East => WorldDirection.North,
					_ => Heading
				};
			}
		}

		public void ChangeLevel(int deltaZ)
		{
			if (ViewLocation != null)
			{
				ViewLocation = ViewLocation.FromDelta(0, 0, deltaZ);
				if (Player != null)
				{
					var destination = new WorldLocation(ViewLocation.X, ViewLocation.Y, ViewLocation.Z);
					World.MoveEntity(Player.EntityId, destination);
				}
			}
		}

		public void JumpToRandomLocation()
		{
			var locationCount = World.EntitiesByLocation.Keys.Count;
			if (locationCount == 0)
				return;

			var locations = new System.Collections.Generic.List<WorldLocation>(World.EntitiesByLocation.Keys);
			ViewLocation = locations[rand.Next(0, locationCount)];
		}
	}
}

