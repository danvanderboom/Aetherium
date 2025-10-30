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
		public string? WorldId { get; set; } // Multi-world support - which world is this session in?
		public World World { get; set; }
		public Character? Player { get; set; }
		public WorldLocation? ViewLocation { get; set; }
		
		/// <summary>
		/// Heading in degrees (0-359). 0 = North, 90 = East, 180 = South, 270 = West.
		/// </summary>
		public int HeadingDegrees { get; set; } = 0;
		
		/// <summary>
		/// Legacy cardinal direction property for backwards compatibility.
		/// Returns the nearest cardinal direction based on HeadingDegrees.
		/// </summary>
		public ConsoleGame.WorldDirection Heading
		{
			get => DegreesToWorldDirection(HeadingDegrees);
			set => HeadingDegrees = WorldDirectionToDegrees(value);
		}
		
		/// <summary>
		/// Whether directional vision mode is enabled.
		/// When true, characters can only see within a forward-facing cone.
		/// When false, omnidirectional vision is used.
		/// </summary>
		public bool DirectionalVisionMode { get; set; } = false;
		
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

	/// <summary>
	/// Creates a session with a world builder (legacy single-world mode).
	/// </summary>
	public GameSession(string connectionId, WorldBuilder worldBuilder)
	{
		ConnectionId = connectionId;
		perceptionService = new PerceptionService();
		GameStartTime = DateTime.UtcNow;
		HeatTracker = new HeatTrailTracker();

		// Build the world
		World = worldBuilder.Build();

		InitializePlayer(worldBuilder);
	}

	/// <summary>
	/// Creates a session with an existing world (multi-world mode).
	/// </summary>
	public GameSession(string connectionId, string worldId, World world, WorldLocation? startLocation = null)
	{
		ConnectionId = connectionId;
		WorldId = worldId;
		World = world;
		perceptionService = new PerceptionService();
		GameStartTime = DateTime.UtcNow;
		HeatTracker = new HeatTrailTracker();

		InitializePlayer(null, startLocation);
	}

	/// <summary>
	/// Initializes the player at the start location.
	/// </summary>
	private void InitializePlayer(WorldBuilder? worldBuilder, WorldLocation? startLocation = null)
	{
		// Determine start location
		if (startLocation == null)
		{
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

		// Determine FOV degrees for directional vision
		int? fovDegrees = null;
		if (DirectionalVisionMode && Player != null)
		{
			// Try to get FOV from player's HasHeading component, or use default
			var hasHeading = Player.Get<HasHeading>();
			fovDegrees = hasHeading?.FieldOfViewDegrees ?? 120; // Default 120 for humans
		}

		return perceptionService.ComputePerception(
			World, 
			ViewLocation, 
			Heading, 
			ViewportSize,
			CurrentLightingMode,
			CurrentVisionMode,
			HeatTracker,
			GetCurrentGameTime(),
			DirectionalVisionMode,
			DirectionalVisionMode ? (int?)HeadingDegrees : null,
			fovDegrees);
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
			// Rotate by 90 degrees
			RotateView(clockwise ? 90 : -90);
		}

		/// <summary>
		/// Rotates the view by a specific number of degrees.
		/// Positive values rotate clockwise, negative values rotate counter-clockwise.
		/// </summary>
		public void RotateView(int degrees)
		{
			HeadingDegrees = (HeadingDegrees + degrees) % 360;
			if (HeadingDegrees < 0)
				HeadingDegrees += 360;
		}

		/// <summary>
		/// Converts degrees to the nearest cardinal WorldDirection.
		/// </summary>
		private static ConsoleGame.WorldDirection DegreesToWorldDirection(int degrees)
		{
			int normalized = degrees % 360;
			if (normalized < 0) normalized += 360;

			if (normalized < 45 || normalized >= 315)
				return ConsoleGame.WorldDirection.North;
			else if (normalized >= 45 && normalized < 135)
				return ConsoleGame.WorldDirection.East;
			else if (normalized >= 135 && normalized < 225)
				return ConsoleGame.WorldDirection.South;
			else
				return ConsoleGame.WorldDirection.West;
		}

		/// <summary>
		/// Converts a cardinal WorldDirection to degrees.
		/// </summary>
		private static int WorldDirectionToDegrees(ConsoleGame.WorldDirection direction)
		{
			return direction switch
			{
				ConsoleGame.WorldDirection.North => 0,
				ConsoleGame.WorldDirection.East => 90,
				ConsoleGame.WorldDirection.South => 180,
				ConsoleGame.WorldDirection.West => 270,
				_ => 0
			};
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

