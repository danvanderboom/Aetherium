using System;
using System.Drawing;
using System.IO;
using Aetherium;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.WorldBuilders;
using Aetherium.Model;
using Aetherium.Server.Perception;

namespace Aetherium.Server
{
	public class GameSession
	{
		public string SessionId { get; set; } = Guid.NewGuid().ToString();
		public string ConnectionId { get; set; } = string.Empty;
		public string? WorldId { get; set; } // Multi-world support - which world is this session in?
		public string? MapId { get; set; }  // Phase 2c: which map within that world

		/// <summary>
		/// The mutation gateway this session uses for gameplay verbs. Set by
		/// <c>GameHub.JoinWorld</c> to a <see cref="Aetherium.Server.MultiWorld.GrainMutationGateway"/>
		/// when the session is grain-bound; left null for legacy sessions, in which case
		/// <c>GameHub.ExecuteTool</c> builds a <see cref="Aetherium.Server.MultiWorld.LocalMutationGateway"/>
		/// on demand.
		/// </summary>
		public Aetherium.Server.MultiWorld.IMapMutationGateway? Gateway { get; set; }
		public World World { get; set; }
		public Character? Player { get; set; }
		public WorldLocation? ViewLocation { get; set; }
		
		/// <summary>
		/// Heading in degrees (0-359). 0 = North, 90 = East, 180 = South, 270 = West.
		///
		/// <para>
		/// As of phase 2, heading is server-authoritative on the player's
		/// <c>Character</c> entity (the <c>HasHeading</c> component). This property
		/// reads through to <c>Player.Get&lt;HasHeading&gt;().Heading</c> when a
		/// Player is bound; otherwise it falls back to the local
		/// <see cref="_fallbackHeadingDegrees"/> for sessions that haven't yet
		/// initialized a Player. The change keeps the perception-pure design
		/// principle: a character's facing direction is objective reality of the
		/// world, not session-local rendering state.
		/// </para>
		/// </summary>
		public int HeadingDegrees
		{
			get => Player?.Get<Aetherium.Components.HasHeading>()?.Heading ?? _fallbackHeadingDegrees;
			set
			{
				var heading = Player?.Get<Aetherium.Components.HasHeading>();
				if (heading is not null)
					heading.Heading = value;
				else
					_fallbackHeadingDegrees = ((value % 360) + 360) % 360;
			}
		}

		/// <summary>
		/// Stores the heading for sessions that don't yet have a Player Character
		/// (during construction, or in tests that build sessions without players).
		/// Once a Player is attached, reads/writes route to its HasHeading component.
		/// </summary>
		private int _fallbackHeadingDegrees = 0;
		
		/// <summary>
		/// Legacy cardinal direction property for backwards compatibility.
		/// Returns the nearest cardinal direction based on HeadingDegrees.
		/// </summary>
		public Aetherium.WorldDirection Heading
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
	/// Serializes state-mutating operations on this session. SignalR can invoke hub
	/// methods in parallel for a single connection, so without this lock concurrent
	/// MoveView/RotateView/GetPerception calls would interleave reads and writes of
	/// ViewLocation/HeadingDegrees/HeatTracker. We deliberately use a coarse lock
	/// rather than per-field synchronization: the critical sections are short, the
	/// reads (GetPerception) and writes (move/rotate/level) need consistent snapshots,
	/// and this becomes free once GameMapGrain takes over (grains are single-threaded
	/// by Orleans contract).
	/// </summary>
	private readonly object _stateLock = new();

	/// <summary>
	/// Creates a session with a world builder (legacy single-world mode).
	/// </summary>
	public GameSession(string connectionId, WorldBuilder worldBuilder, PerceptionService? perceptionService = null)
	{
		ConnectionId = connectionId;
		this.perceptionService = perceptionService ?? new PerceptionService();
		GameStartTime = DateTime.UtcNow;
		HeatTracker = new HeatTrailTracker();

		// Build the world
		World = worldBuilder.Build();

		InitializePlayer(worldBuilder);
	}

	/// <summary>
	/// Creates a session with an existing world (multi-world mode).
	/// </summary>
	public GameSession(string connectionId, string worldId, World world, WorldLocation? startLocation = null, PerceptionService? perceptionService = null)
	{
		ConnectionId = connectionId;
		WorldId = worldId;
		World = world;
		this.perceptionService = perceptionService ?? new PerceptionService();
		GameStartTime = DateTime.UtcNow;
		HeatTracker = new HeatTrailTracker();

		InitializePlayer(null, startLocation);
	}

	/// <summary>
	/// Atomically swap the session's <see cref="World"/> to a freshly hydrated one
	/// (typically from <see cref="WorldBuilders.SnapshotWorldBuilder"/>). Re-initializes
	/// the player at <paramref name="spawnLocation"/>, clears the heat tracker, and
	/// re-anchors <see cref="ViewLocation"/>. Holds the session state lock so no
	/// in-flight gameplay call sees a torn view of the swap.
	///
	/// <para>
	/// PHASE 1 semantics: the new World is independent of the grain's canonical
	/// world. Mutations on it do not propagate to the grain or to other sessions
	/// hydrated from the same snapshot. Phase 2 will replace this with grain-
	/// authoritative mutation and per-session delta-based mirrors.
	/// </para>
	/// </summary>
	public void ReplaceWorld(WorldBuilder builder, string worldId, string mapId, WorldLocation spawnLocation)
	{
		if (builder is null) throw new ArgumentNullException(nameof(builder));
		if (worldId is null) throw new ArgumentNullException(nameof(worldId));
		if (spawnLocation is null) throw new ArgumentNullException(nameof(spawnLocation));

		// Build outside the lock so we don't block in-flight calls during the
		// (potentially millisecond-scale) regen; swap under the lock.
		var newWorld = builder.Build();

		lock (_stateLock)
		{
			World = newWorld;
			WorldId = worldId;
			MapId = mapId;

			HeatTracker = new HeatTrailTracker();
			ViewLocation = new WorldLocation(spawnLocation.X, spawnLocation.Y, spawnLocation.Z);

			// Phase 2c: the player Character is grain-owned. The grain creates it
			// with EntityId == SessionId at JoinPlayerAsync time. The session's local
			// mirror needs a matching Character so perception and inventory work.
			Player = new Character { EntityId = SessionId };
			Player.Set(new WorldLocation(spawnLocation.X, spawnLocation.Y, spawnLocation.Z));
			Player.Set(new Inventory());
			Player.Set(new HasHeading { Heading = 0 });
			World.AddEntity(Player);
		}
	}

	/// <summary>
	/// Reconciles the session's local <see cref="World"/> mirror to reflect a
	/// grain-emitted <see cref="Aetherium.Server.MultiWorld.MapDelta"/>. Holds
	/// the session's existing state lock so applying a delta is consistent with
	/// concurrent perception reads.
	///
	/// <para>
	/// Phase 2c: delta types are dispatched on runtime type. Unknown delta types
	/// or missing entities are logged and dropped — the next periodic perception
	/// resync will heal any divergence.
	/// </para>
	/// </summary>
	public void ApplyDelta(Aetherium.Server.MultiWorld.MapDelta delta)
	{
		if (delta is null) return;

		lock (_stateLock)
		{
			switch (delta)
			{
				case Aetherium.Server.MultiWorld.EntityMovedDelta moved:
					ApplyEntityMoved(moved);
					break;

				case Aetherium.Server.MultiWorld.EntityRemovedDelta removed:
					ApplyEntityRemoved(removed);
					break;

				case Aetherium.Server.MultiWorld.EntityAddedDelta added:
					ApplyEntityAdded(added);
					break;

				case Aetherium.Server.MultiWorld.EntityHeadingChangedDelta headingChanged:
					ApplyHeadingChanged(headingChanged);
					break;

				case Aetherium.Server.MultiWorld.DoorStateChangedDelta doorChanged:
					ApplyDoorStateChanged(doorChanged);
					break;

				case Aetherium.Server.MultiWorld.ItemTransferredDelta transfer:
					ApplyItemTransferred(transfer);
					break;

				case Aetherium.Server.MultiWorld.HeatRecordedDelta heat:
					// Look up the entity in the local mirror for its HeatSignature
					// (which has the Duration we need for fade calculations). If the
					// entity isn't in the mirror (e.g. another player out of FOV), use
					// the delta's intensity with a default duration. Heat data is
					// observable independently of entity visibility.
					var heatLoc = new WorldLocation(heat.X, heat.Y, heat.Z);
					var heatTimestamp = GameStartTime.AddHours(heat.GameTimeHours);
					if (World.Entities.TryGetValue(heat.EntityId, out var heatEntity))
					{
						HeatTracker.RecordEntityPosition(heatEntity, heatLoc, heatTimestamp);
					}
					else
					{
						HeatTracker.RecordRaw(
							heat.EntityId,
							heatLoc,
							heatTimestamp,
							heat.Intensity,
							TimeSpan.FromSeconds(10)); // default duration when source entity isn't mirrored
					}
					break;

				case Aetherium.Server.MultiWorld.HeatExpiredDelta expired:
					HeatTracker.RemoveTrailsAt(new WorldLocation(expired.X, expired.Y, expired.Z));
					break;

				case Aetherium.Server.MultiWorld.ComponentFieldChangedDelta fieldChanged:
					ApplyComponentFieldChanged(fieldChanged);
					break;

				case Aetherium.Server.MultiWorld.ItemDestroyedDelta destroyed:
					ApplyItemDestroyed(destroyed);
					break;

				case Aetherium.Server.MultiWorld.EntityPlacedDelta placed:
					ApplyEntityPlaced(placed);
					break;

				default:
					Console.WriteLine($"[GameSession] Ignoring unknown delta type {delta.GetType().Name}");
					break;
			}
		}
	}

	private void ApplyComponentFieldChanged(Aetherium.Server.MultiWorld.ComponentFieldChangedDelta delta)
	{
		// Find the entity in the world mirror OR in any owner's inventory mirror.
		// Items reduced by Use (Consumable.Uses, Lockpick.Durability) live in inventory.
		var entity = FindEntityAnywhere(delta.EntityId);
		if (entity is null) return;

		// Dispatch on (ComponentType, FieldName). New mutable fields must be added
		// here as well as on the emitting grain side. Unknown pairs throw so test
		// failures are loud rather than silent.
		var key = (delta.ComponentType, delta.FieldName);
		switch (key)
		{
			case ("Consumable", "Uses"):
				{
					var c = entity.Get<Aetherium.Components.Consumable>();
					if (c is not null && delta.NumericValue.HasValue) c.Uses = (int)delta.NumericValue.Value;
					break;
				}
			case ("Health", "Level"):
				{
					var h = entity.Get<Aetherium.Components.Health>();
					if (h is not null && delta.NumericValue.HasValue) h.Level = (int)delta.NumericValue.Value;
					break;
				}
			case ("ForcesDoor", "Durability"):
				{
					var c = entity.Get<Aetherium.Components.ForcesDoor>();
					if (c is not null && delta.NumericValue.HasValue) c.Durability = (int)delta.NumericValue.Value;
					break;
				}
			case ("Lockpick", "Durability"):
				{
					var c = entity.Get<Aetherium.Components.Lockpick>();
					if (c is not null && delta.NumericValue.HasValue) c.Durability = (int)delta.NumericValue.Value;
					break;
				}
			case ("PlaceableLight", "IsPlaced"):
				{
					var c = entity.Get<Aetherium.Components.PlaceableLight>();
					if (c is not null && delta.BoolValue.HasValue) c.IsPlaced = delta.BoolValue.Value;
					break;
				}
			case ("LightSource", "IsEnabled"):
				{
					var c = entity.Get<Aetherium.Components.LightSource>();
					if (c is not null && delta.BoolValue.HasValue) c.IsEnabled = delta.BoolValue.Value;
					break;
				}
			case ("LightSource", "IsDynamic"):
				{
					var c = entity.Get<Aetherium.Components.LightSource>();
					if (c is not null && delta.BoolValue.HasValue) c.IsDynamic = delta.BoolValue.Value;
					break;
				}
			case ("Activatable", "IsActivated"):
				{
					var c = entity.Get<Aetherium.Components.Activatable>();
					if (c is not null && delta.BoolValue.HasValue) c.IsActivated = delta.BoolValue.Value;
					break;
				}
			case ("Inventory", "Capacity"):
				{
					var c = entity.Get<Aetherium.Components.Inventory>();
					if (c is not null && delta.NumericValue.HasValue) c.Capacity = (int)delta.NumericValue.Value;
					break;
				}
			default:
				throw new NotImplementedException(
					$"ComponentFieldChangedDelta for ({delta.ComponentType}.{delta.FieldName}) is not handled. " +
					"Add a case in GameSession.ApplyComponentFieldChanged.");
		}
	}

	private void ApplyItemDestroyed(Aetherium.Server.MultiWorld.ItemDestroyedDelta delta)
	{
		if (!string.IsNullOrEmpty(delta.OwnerEntityId))
		{
			// Owner-side destruction: remove from that character's inventory.
			if (World.Entities.TryGetValue(delta.OwnerEntityId, out var owner))
			{
				var inv = owner.Get<Aetherium.Components.Inventory>();
				inv?.Remove(delta.EntityId);
			}
			else if (Player is not null && Player.EntityId == delta.OwnerEntityId)
			{
				var inv = Player.Get<Aetherium.Components.Inventory>();
				inv?.Remove(delta.EntityId);
			}
		}
		else
		{
			// World-side destruction.
			World.TryRemoveEntity(delta.EntityId);
		}
	}

	private void ApplyEntityPlaced(Aetherium.Server.MultiWorld.EntityPlacedDelta delta)
	{
		if (delta.Placement is null) return;

		// Remove from the source owner's inventory before placing in the world to
		// avoid the item briefly existing in both mirrors.
		if (!string.IsNullOrEmpty(delta.SourceOwnerEntityId))
		{
			Aetherium.Components.Inventory? sourceInv = null;
			if (Player is not null && Player.EntityId == delta.SourceOwnerEntityId)
				sourceInv = Player.Get<Aetherium.Components.Inventory>();
			else if (World.Entities.TryGetValue(delta.SourceOwnerEntityId, out var owner))
				sourceInv = owner.Get<Aetherium.Components.Inventory>();
			sourceInv?.Remove(delta.Placement.EntityId);
		}

		if (World.Entities.ContainsKey(delta.Placement.EntityId))
			return; // idempotent

		var factory = new Aetherium.Server.MultiWorld.EntityFactory(World);
		var entity = factory.Create(delta.Placement);
		if (entity is not null)
			World.AddEntity(entity);
	}

	/// <summary>
	/// Look up an entity by id in either the world mirror or any character's
	/// inventory mirror. ComponentFieldChangedDelta targets items in inventory
	/// (Consumable, Lockpick) as well as world entities (Activatable, doors).
	/// </summary>
	private Aetherium.Core.Entity? FindEntityAnywhere(string entityId)
	{
		if (World.Entities.TryGetValue(entityId, out var worldEntity))
			return worldEntity;

		// Check our player's inventory.
		if (Player is not null)
		{
			var inv = Player.Get<Aetherium.Components.Inventory>();
			if (inv is not null && inv.Items.TryGetValue(entityId, out var item))
				return item;
		}

		// Check other characters' inventories (rare in single-player; matters for shared rooms).
		foreach (var e in World.Entities.Values)
		{
			var inv = e.Get<Aetherium.Components.Inventory>();
			if (inv is not null && inv.Items.TryGetValue(entityId, out var item))
				return item;
		}

		return null;
	}

	private void ApplyEntityMoved(Aetherium.Server.MultiWorld.EntityMovedDelta delta)
	{
		// Skip the player's own move — the gateway-route already advanced ViewLocation
		// (in legacy mode) or this is a no-op (in grain mode the entity moves via the
		// grain; the local mirror just needs the index update).
		if (!World.Entities.TryGetValue(delta.EntityId, out _))
			return;
		World.MoveEntity(delta.EntityId, new WorldLocation(delta.NewX, delta.NewY, delta.NewZ));

		// If this delta concerns the player Character, keep ViewLocation in sync.
		// This is the grain-routed session's own-move path, so it advances the move
		// sequence exactly like local MoveView does.
		if (Player is not null && delta.EntityId == Player.EntityId)
		{
			ViewLocation = new WorldLocation(delta.NewX, delta.NewY, delta.NewZ);
			_moveSequence++;
		}
	}

	private void ApplyEntityRemoved(Aetherium.Server.MultiWorld.EntityRemovedDelta delta)
	{
		World.TryRemoveEntity(delta.EntityId);
	}

	private void ApplyEntityAdded(Aetherium.Server.MultiWorld.EntityAddedDelta delta)
	{
		if (delta.Placement is null) return;
		if (World.Entities.ContainsKey(delta.Placement.EntityId))
			return; // already present (e.g. our own add); idempotent

		var factory = new Aetherium.Server.MultiWorld.EntityFactory(World);
		var entity = factory.Create(delta.Placement);
		if (entity is not null)
			World.AddEntity(entity);
	}

	private void ApplyHeadingChanged(Aetherium.Server.MultiWorld.EntityHeadingChangedDelta delta)
	{
		// Heading deltas are sent only to the actor's own session. Update the local
		// mirror Character's HasHeading so the actor's perception cone reflects it.
		if (!World.Entities.TryGetValue(delta.EntityId, out var entity))
			return;
		var heading = entity.Get<HasHeading>();
		if (heading is not null)
			heading.Heading = delta.Degrees;
	}

	private void ApplyDoorStateChanged(Aetherium.Server.MultiWorld.DoorStateChangedDelta delta)
	{
		if (!World.Entities.TryGetValue(delta.EntityId, out var entity))
			return;
		var oc = entity.Get<OpensAndCloses>();
		if (oc is not null)
		{
			oc.IsOpen = delta.IsOpen;
			oc.IsLocked = delta.IsLocked;
		}
	}

	private void ApplyItemTransferred(Aetherium.Server.MultiWorld.ItemTransferredDelta delta)
	{
		if (delta.IntoInventory)
		{
			// World → inventory. Remove from world, add to owner's inventory.
			World.TryRemoveEntity(delta.ItemEntityId);
			if (Player is not null && delta.OwnerEntityId == Player.EntityId)
			{
				var inv = Player.Get<Inventory>();
				if (inv is not null && delta.ItemPlacement is not null)
				{
					var factory = new Aetherium.Server.MultiWorld.EntityFactory(World);
					var item = factory.Create(delta.ItemPlacement);
					if (item is not null)
						inv.TryAdd(item.EntityId, item);
				}
			}
		}
		else
		{
			// Inventory → world. Remove from owner's inventory (if it's us), add to world.
			if (Player is not null && delta.OwnerEntityId == Player.EntityId)
			{
				var inv = Player.Get<Inventory>();
				if (inv is not null && inv.Items.TryGetValue(delta.ItemEntityId, out var item))
				{
					inv.Remove(delta.ItemEntityId);
					item.Set(new WorldLocation(delta.X, delta.Y, delta.Z));
					if (!World.Entities.ContainsKey(item.EntityId))
						World.AddEntity(item);
				}
			}
		}
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

	/// <summary>
	/// Runs <paramref name="action"/> under this session's state lock, serializing it
	/// against movement, rotation, level changes, and <see cref="GetPerception"/>
	/// snapshots. Interaction verbs (pickup/drop/use/open/close) route through here so
	/// that, for legacy (non-grain-bound) sessions, a SignalR hub call and a
	/// <c>GameManagementGrain</c> call can no longer interleave mutations of the same
	/// World/Player — closing the cross-path races. Grain-bound sessions are already
	/// serialized by Orleans' single-threaded grain contract, so their gateway
	/// (<c>GrainMutationGateway</c>) does not need this. The lock is reentrant
	/// (Monitor), so nesting inside another locked section on the same thread is safe.
	/// </summary>
	public T WithStateLock<T>(Func<T> action)
	{
		lock (_stateLock)
			return action();
	}

	// Monotonic count of the player's own successful anchor-changing moves (steps and
	// level changes), stamped on every perception frame as MoveSequence. Starts at 1 so
	// a real frame can never carry 0 (0 = legacy/unsequenced). This is what lets a
	// client order frames against its own movement: a frame computed before a move but
	// delivered after its response (tick/tool races share one connection) carries the
	// old count and can be recognized as stale instead of corrupting client-side
	// position-anchored state. Always mutated under _stateLock, the same lock that
	// guards ViewLocation and GetPerception — (location, sequence) stay consistent.
	private long _moveSequence = 1;

	public Aetherium.Model.PerceptionDto GetPerception()
	{
		lock (_stateLock)
		{
			if (ViewLocation == null)
				throw new InvalidOperationException("ViewLocation is null");

			// Heat is grain-authoritative and flows in via ApplyDelta. No per-perception
			// iteration here — the local mirror is kept up to date by the host-side
			// delta broker.

			// Determine FOV degrees for directional vision
			int? fovDegrees = null;
			if (DirectionalVisionMode && Player != null)
			{
				// Try to get FOV from player's HasHeading component, or use default
				var hasHeading = Player.Get<HasHeading>();
				fovDegrees = hasHeading?.FieldOfViewDegrees ?? 120; // Default 120 for humans
			}

			// Passing the session's player as `self` populates the interoception channel on
			// every hub-pushed frame (add-interoception-channel) — the same wiring the grain's
			// agent path has. For local sessions the World is authoritative; for grain-bound
			// sessions the mirror player's Health stays current via ("Health","Level") deltas
			// in ApplyDelta (statuses/pools mirror as those deltas are added).
			var perception = perceptionService.ComputePerception(
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
				fovDegrees,
				self: Player);
			perception.MoveSequence = _moveSequence;
			return perception;
		}
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

		public Aetherium.Core.MoveOutcome MoveView(Aetherium.Model.RelativeDirection direction, int distance = 1)
		{
			lock (_stateLock)
			{
				if (ViewLocation == null)
					return Aetherium.Core.MoveOutcome.Blocked(null, "No view location");

				var engineDirection = direction.ToEngineRelativeDirection();

				var rightAngleRotationsCounterclockwise = engineDirection switch
				{
					Aetherium.RelativeDirection.Forward => 0,
					Aetherium.RelativeDirection.Left => 1,
					Aetherium.RelativeDirection.Backward => 2,
					Aetherium.RelativeDirection.Right => 3,
					_ => 0
				};

				var bearing = Heading;
				for (int i = 0; i < rightAngleRotationsCounterclockwise; i++)
					bearing = bearing.RotateLeft();

				if (Player != null)
				{
					// Player-backed session: the character is authoritative. Every
					// step is validated by the engine (walls, closed doors, other
					// characters, map bounds) and the view follows wherever the
					// character actually ended up — the camera can no longer drift
					// through geometry the player couldn't cross.
					var outcome = World.TryMoveSteps(Player, bearing, distance);
					if (outcome.FinalLocation != null)
						ViewLocation = new WorldLocation(
							outcome.FinalLocation.X, outcome.FinalLocation.Y, outcome.FinalLocation.Z);
					if (outcome.Success)
						_moveSequence++;
					return outcome;
				}

				// Observer session with no character: the view is a free camera and
				// nothing world-authoritative moves, so no validation applies.
				ViewLocation = bearing switch
				{
					Aetherium.WorldDirection.North => ViewLocation.FromDelta(0, -distance, 0),
					Aetherium.WorldDirection.East => ViewLocation.FromDelta(distance, 0, 0),
					Aetherium.WorldDirection.South => ViewLocation.FromDelta(0, distance, 0),
					Aetherium.WorldDirection.West => ViewLocation.FromDelta(-distance, 0, 0),
					_ => ViewLocation
				};
				_moveSequence++;
				return new Aetherium.Core.MoveOutcome
				{
					Success = true,
					StepsTaken = distance,
					FinalLocation = ViewLocation,
				};
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
			lock (_stateLock)
			{
				HeadingDegrees = (HeadingDegrees + degrees) % 360;
				if (HeadingDegrees < 0)
					HeadingDegrees += 360;
			}
		}

		/// <summary>
		/// Converts degrees to the nearest cardinal WorldDirection.
		/// </summary>
		private static Aetherium.WorldDirection DegreesToWorldDirection(int degrees)
		{
			int normalized = degrees % 360;
			if (normalized < 0) normalized += 360;

			if (normalized < 45 || normalized >= 315)
				return Aetherium.WorldDirection.North;
			else if (normalized >= 45 && normalized < 135)
				return Aetherium.WorldDirection.East;
			else if (normalized >= 135 && normalized < 225)
				return Aetherium.WorldDirection.South;
			else
				return Aetherium.WorldDirection.West;
		}

		/// <summary>
		/// Converts a cardinal WorldDirection to degrees.
		/// </summary>
		private static int WorldDirectionToDegrees(Aetherium.WorldDirection direction)
		{
			return direction switch
			{
				Aetherium.WorldDirection.North => 0,
				Aetherium.WorldDirection.East => 90,
				Aetherium.WorldDirection.South => 180,
				Aetherium.WorldDirection.West => 270,
				_ => 0
			};
		}

		public Aetherium.Core.MoveOutcome ChangeLevel(int deltaZ)
		{
			lock (_stateLock)
			{
				if (ViewLocation == null)
					return Aetherium.Core.MoveOutcome.Blocked(null, "No view location");

				if (Player != null)
				{
					// Player-backed session: level changes require standing on a
					// stair cell and a valid landing (see World.TryChangeLevel) —
					// no more teleporting between floors or into the void.
					var outcome = World.TryChangeLevel(Player, deltaZ);
					if (outcome.FinalLocation != null)
						ViewLocation = new WorldLocation(
							outcome.FinalLocation.X, outcome.FinalLocation.Y, outcome.FinalLocation.Z);
					if (outcome.Success)
						_moveSequence++;
					return outcome;
				}

				// Observer session: free camera, no validation.
				ViewLocation = ViewLocation.FromDelta(0, 0, deltaZ);
				_moveSequence++;
				return new Aetherium.Core.MoveOutcome
				{
					Success = true,
					StepsTaken = System.Math.Abs(deltaZ),
					FinalLocation = ViewLocation,
				};
			}
		}

		public void JumpToRandomLocation()
		{
			lock (_stateLock)
			{
				var locationCount = World.EntitiesByLocation.Keys.Count;
				if (locationCount == 0)
					return;

				var locations = new System.Collections.Generic.List<WorldLocation>(World.EntitiesByLocation.Keys);
				ViewLocation = locations[rand.Next(0, locationCount)];
			}
		}
	}
}


