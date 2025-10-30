using System;
using System.Drawing;
using System.IO;
using ConsoleGame;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGame.Entities;
using ConsoleGame.WorldBuilders;
using ConsoleGameModel;
using ConsoleGameServer.Perception;

namespace ConsoleGameServer
{
	public class GameSession
	{
		public string SessionId { get; set; } = Guid.NewGuid().ToString();
		public string ConnectionId { get; set; } = string.Empty;
		public World World { get; set; }
		public Character? Player { get; set; }
		public WorldLocation? ViewLocation { get; set; }
		public ConsoleGame.WorldDirection Heading { get; set; } = ConsoleGame.WorldDirection.North;
		public Size ViewportSize { get; set; } = new Size(42, 22); // Default console size

		// Game time tracking
		public DateTime GameStartTime { get; private set; }
		public double TimeScale { get; set; } = 60.0; // 60x real time (1 real minute = 1 game hour)

		// Lighting and vision modes
		public LightingMode CurrentLightingMode { get; set; } = LightingMode.Torch;
		public VisionMode CurrentVisionMode { get; set; } = VisionMode.Normal;

		// Heat trail tracking for infrared vision
		public HeatTrailTracker HeatTracker { get; private set; }

		private readonly PerceptionService perceptionService;
		private readonly Random rand = new Random();

		public GameSession(string connectionId, WorldBuilder worldBuilder)
		{
			ConnectionId = connectionId;
			perceptionService = new PerceptionService();
			GameStartTime = DateTime.UtcNow;
			HeatTracker = new HeatTrailTracker();

			// Build the world
			World = worldBuilder.Build();

            // Determine start location
            WorldLocation? startLocation = null;
            if (worldBuilder is AudioTestWorldBuilder audioBuilder)
            {
                startLocation = audioBuilder.StartLocation;
            }
            else if (worldBuilder is FovDiagnosticWorldBuilder fovBuilder && fovBuilder.StartLocation != null)
            {
                startLocation = fovBuilder.StartLocation;
            }
            else
            {
                startLocation = World.SelectRandomPassableLocation();
            }


			// Initialize view location and player
			if (startLocation is WorldLocation loc)
			{
				ViewLocation = loc;
				Player = new Character();
				Player.Set(new WorldLocation(loc.X, loc.Y, loc.Z));
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

			// Update heat tracker before computing perception
			UpdateHeatTracker();

			return perceptionService.ComputePerception(
				World, 
				ViewLocation, 
				Heading, 
				ViewportSize,
				CurrentLightingMode,
				CurrentVisionMode,
				HeatTracker,
				GetCurrentGameTime());
		}

		/// <summary>
		/// Gets the current game time based on elapsed real time and time scale
		/// </summary>
		public DateTime GetCurrentGameTime()
		{
			var realElapsed = DateTime.UtcNow - GameStartTime;
			var gameElapsed = TimeSpan.FromSeconds(realElapsed.TotalSeconds * TimeScale);
			return GameStartTime.Add(gameElapsed);
		}

		/// <summary>
		/// Gets the current time of day as hours (0-24)
		/// </summary>
		public double GetGameTimeOfDay()
		{
			var gameTime = GetCurrentGameTime();
			return gameTime.TimeOfDay.TotalHours % 24.0;
		}

	/// <summary>
	/// Updates heat tracker with current entity positions
	/// </summary>
	private void UpdateHeatTracker()
	{
		var currentTime = GetCurrentGameTime();

		// Record heat signatures for all entities with HeatSignature component
		foreach (var entity in World.Entities.Values)
		{
			// Skip entities that don't have required components
			if (!entity.Has<WorldLocation>() || !entity.Has<HeatSignature>())
				continue;

			var location = entity.Get<WorldLocation>();
			var heatSig = entity.Get<HeatSignature>();
			
			HeatTracker.RecordEntityPosition(entity, location, currentTime);
		}

		// Cleanup old trails (remove those older than 60 seconds)
		HeatTracker.CleanupOldTrails(currentTime.AddSeconds(-60));
	}

		public void MoveView(ConsoleGameModel.RelativeDirection direction, int distance = 1)
		{
			if (ViewLocation == null)
				return;

			var engineDirection = direction.ToEngineRelativeDirection();

			var rightAngleRotationsCounterclockwise = engineDirection switch
			{
				ConsoleGame.RelativeDirection.Forward => 0,
				ConsoleGame.RelativeDirection.Left => 1,
				ConsoleGame.RelativeDirection.Backward => 2,
				ConsoleGame.RelativeDirection.Right => 3,
				_ => 0
			};

			var bearing = Heading;
			for (int i = 0; i < rightAngleRotationsCounterclockwise; i++)
				bearing = bearing.RotateLeft();

			ViewLocation = bearing switch
			{
				ConsoleGame.WorldDirection.North => ViewLocation.FromDelta(0, -distance, 0),
				ConsoleGame.WorldDirection.East => ViewLocation.FromDelta(distance, 0, 0),
				ConsoleGame.WorldDirection.South => ViewLocation.FromDelta(0, distance, 0),
				ConsoleGame.WorldDirection.West => ViewLocation.FromDelta(-distance, 0, 0),
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
					ConsoleGame.WorldDirection.North => ConsoleGame.WorldDirection.East,
					ConsoleGame.WorldDirection.East => ConsoleGame.WorldDirection.South,
					ConsoleGame.WorldDirection.South => ConsoleGame.WorldDirection.West,
					ConsoleGame.WorldDirection.West => ConsoleGame.WorldDirection.North,
					_ => Heading
				};
			}
			else
			{
				Heading = Heading switch
				{
					ConsoleGame.WorldDirection.North => ConsoleGame.WorldDirection.West,
					ConsoleGame.WorldDirection.West => ConsoleGame.WorldDirection.South,
					ConsoleGame.WorldDirection.South => ConsoleGame.WorldDirection.East,
					ConsoleGame.WorldDirection.East => ConsoleGame.WorldDirection.North,
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

