using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.WorldBuilders;

namespace Aetherium.WorldGen.Generators
{
    /// <summary>
    /// Feature-rich dungeon generator that produces multi-level layouts with varied room shapes,
    /// loops, secrets, gating, and interactive elements required for advanced agent experiences.
    /// </summary>
    public sealed class AdvancedDungeonGenerator : IMapGenerator
    {
        private readonly TestMazeWorldBuilder _baseBuilder = new();

        public World Generate(GeneratorContext context)
        {
            var world = new World();
            RegisterTilesAndTerrains(world);

            var globals = new GenerationGlobals(context);

            for (int level = 0; level < context.Levels; level++)
            {
                FillWithWalls(world, context, level);
                BuildLevel(world, context, globals, level);
            }

            ConnectLevels(world, context, globals);
            CarveCorridors(world, globals);
            BuildPrimaryPath(world, context, globals);
            PlaceGatingAndKeys(world, context, globals);
            PlaceSecrets(world, context, globals);
            PlaceTrapsAndTools(world, context, globals);
            ComputeMetrics(context, globals);

            return world;
        }

        #region Setup

        private void RegisterTilesAndTerrains(World world)
        {
            var tileTypes = _baseBuilder.TileTypes;
            world.AddTileTypes(tileTypes);
            world.AddTerrainTypes(_baseBuilder.CreateTerrainTypes(tileTypes));

            if (!world.TileTypes.ContainsKey("Monster"))
            {
                world.TileTypes["Monster"] = new TileType
                {
                    Name = "Monster",
                    Settings = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "MapCharacter", "M" },
                        { "BackgroundColor", ConsoleColor.Black.ToString() },
                        { "ForegroundColor", ConsoleColor.DarkRed.ToString() }
                    }
                };
            }
        }

        private void FillWithWalls(World world, GeneratorContext context, int level)
        {
            for (int y = 0; y < context.Height; y++)
            {
                for (int x = 0; x < context.Width; x++)
                {
                    world.SetTerrain("Wall", new WorldLocation(x, y, level));
                }
            }
        }

        #endregion

        #region Level Construction

        private void BuildLevel(World world, GeneratorContext context, GenerationGlobals globals, int level)
        {
            var rng = context.GetRandom($"level:{level}:rooms");
            // Room count is drawn from [minRooms, maxRooms] inclusive. Defaults (6, 9) reproduce the
            // historical rng.Next(6, 10) draw exactly, so a request with no room params is unchanged.
            int minRooms = context.GetIntParam("minRooms", 6, min: 1, max: 500);
            int maxRooms = context.GetIntParam("maxRooms", 9, min: minRooms, max: 500);
            int targetRooms = rng.Next(minRooms, maxRooms + 1);
            var layoutHelper = new LayoutHelper(context, level, globals, rng);

            while (layoutHelper.RoomsOnLevel.Count < targetRooms && layoutHelper.Attempts < targetRooms * 12)
            {
                layoutHelper.Attempts++;
                var shape = layoutHelper.GetNextShape();
                var rect = layoutHelper.GetRandomBounds(shape);
                if (!layoutHelper.CanPlace(rect))
                {
                    continue;
                }

                layoutHelper.CreateRoom(world, rect, shape);
            }

            layoutHelper.EnsureShapeVariety(world);
            ConnectRoomsOnLevel(world, context, globals, layoutHelper, rng);
        }

        private void ConnectRoomsOnLevel(World world, GeneratorContext context, GenerationGlobals globals, LayoutHelper helper, Random rng)
        {
            if (helper.RoomsOnLevel.Count <= 1)
                return;

            var candidates = new List<EdgeCandidate>();
            for (int i = 0; i < helper.RoomsOnLevel.Count; i++)
            {
                for (int j = i + 1; j < helper.RoomsOnLevel.Count; j++)
                {
                    var a = helper.RoomsOnLevel[i];
                    var b = helper.RoomsOnLevel[j];
                    var distance = Distance(a.Center, b.Center);
                    candidates.Add(new EdgeCandidate(a, b, distance));
                }
            }

            candidates.Sort((a, b) => a.Weight.CompareTo(b.Weight));

            var ds = new DisjointSet(helper.RoomsOnLevel.Select(r => r.Id));
            var extraEdges = new List<EdgeCandidate>();

            foreach (var candidate in candidates)
            {
                if (ds.Union(candidate.A.Id, candidate.B.Id))
                {
                    CreateCorridor(world, context, globals, candidate.A, candidate.B, rng, primary: true);
                }
                else
                {
                    extraEdges.Add(candidate);
                }
            }

            foreach (var candidate in extraEdges)
            {
                if (rng.NextDouble() < 0.35)
                {
                    CreateCorridor(world, context, globals, candidate.A, candidate.B, rng, primary: false);
                }
            }
        }

        private void CreateCorridor(World world, GeneratorContext context, GenerationGlobals globals, DungeonRoom from, DungeonRoom to, Random rng, bool primary)
        {
            var key = CorridorKey.Create(from.Id, to.Id);
            if (globals.Corridors.ContainsKey(key))
                return;

            var path = CarveLShapedPath(world, context, from.Center, to.Center, from.Level, rng);
            var corridor = new DungeonCorridor(from, to, path, primary);
            globals.Corridors[key] = corridor;
            globals.Graph.AddEdge(from.Id, to.Id);
        }

        private List<WorldLocation> CarveLShapedPath(World world, GeneratorContext context, WorldLocation start, WorldLocation end, int level, Random rng)
        {
            var path = new List<WorldLocation>();
            int x = start.X;
            int y = start.Y;
            int targetX = end.X;
            int targetY = end.Y;

            bool horizontalFirst = rng.NextDouble() > 0.5;

            void Step(int nx, int ny)
            {
                var loc = new WorldLocation(nx, ny, level);
                world.SetTerrain("Indoors", loc);
                path.Add(loc);
            }

            if (horizontalFirst)
            {
                while (x != targetX)
                {
                    x += x < targetX ? 1 : -1;
                    Step(x, y);
                }
                while (y != targetY)
                {
                    y += y < targetY ? 1 : -1;
                    Step(x, y);
                }
            }
            else
            {
                while (y != targetY)
                {
                    y += y < targetY ? 1 : -1;
                    Step(x, y);
                }
                while (x != targetX)
                {
                    x += x < targetX ? 1 : -1;
                    Step(x, y);
                }
            }

            return path;
        }

        #endregion

        #region Post-processing

        private void ConnectLevels(World world, GeneratorContext context, GenerationGlobals globals)
        {
            if (context.Levels <= 1)
                return;

            for (int level = 0; level < context.Levels - 1; level++)
            {
                var upperRooms = globals.Rooms.Where(r => r.Level == level).ToList();
                var lowerRooms = globals.Rooms.Where(r => r.Level == level + 1).ToList();
                if (upperRooms.Count == 0 || lowerRooms.Count == 0)
                    continue;

                var rng = context.GetRandom($"level-connector:{level}");
                var upperRoom = upperRooms[rng.Next(upperRooms.Count)];
                var lowerRoom = lowerRooms[rng.Next(lowerRooms.Count)];

                // The stair pair must be vertically aligned — validated movement
                // (World.TryChangeLevel) moves straight along Z, so the landing
                // cell is the same X/Y one level down. (Previously the two stair
                // cells sat at unrelated room centers, making the "stairs"
                // physically impossible to traverse.)
                var connectorLoc = upperRoom.Center;
                var downstairsLoc = new WorldLocation(connectorLoc.X, connectorLoc.Y, lowerRoom.Level);

                world.SetTerrain("Downstairs", connectorLoc);
                world.SetTerrain("Upstairs", downstairsLoc);

                // The aligned landing may sit inside the lower level's wall mass;
                // carve a corridor from it to the lower room so the stairs connect
                // to the rest of the level.
                var landingPath = CarveLShapedPath(world, context, downstairsLoc, lowerRoom.Center, lowerRoom.Level, rng);

                globals.Graph.AddEdge(upperRoom.Id, lowerRoom.Id);

                var corridorTiles = new List<WorldLocation> { connectorLoc, downstairsLoc };
                corridorTiles.AddRange(landingPath);
                var corridor = new DungeonCorridor(upperRoom, lowerRoom, corridorTiles, primary: true)
                {
                    IsVerticalConnector = true
                };
                globals.Corridors[CorridorKey.Create(upperRoom.Id, lowerRoom.Id)] = corridor;
            }
        }

        private void CarveCorridors(World world, GenerationGlobals globals)
        {
            foreach (var corridor in globals.Corridors.Values)
            {
                foreach (var loc in corridor.Path)
                {
                    world.SetTerrain("Indoors", loc);
                }
            }
        }

        private void BuildPrimaryPath(World world, GeneratorContext context, GenerationGlobals globals)
        {
            var nonSecret = globals.Rooms.Where(r => !r.IsSecret).OrderBy(r => (r.Level, r.Id)).ToList();
            if (nonSecret.Count == 0)
                return;

            var (startRoom, targetRoom) = SelectStartAndObjective(nonSecret, globals.Graph);
            globals.StartRoomId = startRoom.Id;
            globals.ObjectiveRoomId = targetRoom.Id;

            var pathRooms = globals.Graph.FindPath(startRoom.Id, targetRoom.Id);
            if (pathRooms.Count == 0)
                return;

            globals.PrimaryRoomPath.Clear();
            globals.PrimaryRoomPath.AddRange(pathRooms);

            context.StartLocation = startRoom.Center;
            context.ObjectiveLocation = targetRoom.Center;
            context.PrimaryPath.Clear();

            for (int i = 0; i < pathRooms.Count - 1; i++)
            {
                var from = globals.RoomLookup[pathRooms[i]];
                var to = globals.RoomLookup[pathRooms[i + 1]];
                var key = CorridorKey.Create(from.Id, to.Id);
                if (!globals.Corridors.TryGetValue(key, out var corridor))
                    continue;

                context.PrimaryPath.Add(from.Center);
                context.PrimaryPath.AddRange(corridor.Path);
            }
            context.PrimaryPath.Add(globals.RoomLookup[pathRooms.Last()].Center);
        }

        private void PlaceGatingAndKeys(World world, GeneratorContext context, GenerationGlobals globals)
        {
            if (globals.StartRoomId == null || globals.ObjectiveRoomId == null)
                return;

            if (globals.PrimaryRoomPath.Count < 2)
                return;

            var bridgeResult = FindBridgeCorridor(globals, globals.StartRoomId.Value, globals.ObjectiveRoomId.Value);
            if (bridgeResult == null)
                return;

            var corridor = bridgeResult.Value.Corridor;
            var bridgeFromRoom = globals.RoomLookup[bridgeResult.Value.FromId];
            var bridgeToRoom = globals.RoomLookup[bridgeResult.Value.ToId];
            var doorLocation = corridor.Path.Count > 0
                ? corridor.Path[corridor.Path.Count / 2]
                : bridgeToRoom.Center;

            var door = new Door();
            door.Set(doorLocation);
            var openClose = door.Get<OpensAndCloses>();
            openClose.IsLocked = true;
            openClose.KeyShape = "emerald";
            world.AddEntity(door);

            globals.InteractiveArtifacts.LockedDoorLocation = doorLocation;
            context.Metrics.LockedDoors++;

            // Place the key in the start room. For a true bridge edge this guarantees the key is
            // reachable from start without traversing the door. For the fallback case (no true
            // bridge — multiple paths exist), the start room is also trivially reachable.
            var keyLocation = globals.RoomLookup[globals.StartRoomId.Value].Center;
            var key = new KeyItem("emerald");
            key.Set(keyLocation);
            world.AddEntity(key);
            context.Metrics.KeysPlaced++;
        }

        private static (DungeonRoom Start, DungeonRoom Objective) SelectStartAndObjective(
            List<DungeonRoom> nonSecretRooms,
            RoomGraph graph)
        {
            // Constrain start to the shallowest level, objective to the deepest level.
            int firstLevel = nonSecretRooms[0].Level;
            int lastLevel = nonSecretRooms[nonSecretRooms.Count - 1].Level;

            var startCandidates = nonSecretRooms.Where(r => r.Level == firstLevel).ToList();
            var objectiveCandidates = nonSecretRooms.Where(r => r.Level == lastLevel).ToList();

            DungeonRoom? bestStart = null;
            DungeonRoom? bestObjective = null;
            int bestDistance = -1;

            // O(start * (V+E)) — small constant for typical dungeons (<100 rooms).
            // Both lists are already ordered by (Level, Id), so ties resolve deterministically.
            foreach (var start in startCandidates)
            {
                var distances = graph.BfsDistances(start.Id);
                foreach (var objective in objectiveCandidates)
                {
                    if (start.Id == objective.Id)
                        continue;
                    if (!distances.TryGetValue(objective.Id, out var d))
                        continue;
                    if (d > bestDistance)
                    {
                        bestDistance = d;
                        bestStart = start;
                        bestObjective = objective;
                    }
                }
            }

            if (bestStart != null && bestObjective != null)
                return (bestStart, bestObjective);

            // Graph may be disconnected across levels; fall back to deterministic first/last.
            return (startCandidates[0], objectiveCandidates[objectiveCandidates.Count - 1]);
        }

        private (DungeonCorridor Corridor, int FromId, int ToId)? FindBridgeCorridor(GenerationGlobals globals, int startId, int targetId)
        {
            // Preferred: a true bridge edge on the primary path — locking it cuts start from objective.
            for (int i = 0; i < globals.PrimaryRoomPath.Count - 1; i++)
            {
                var fromId = globals.PrimaryRoomPath[i];
                var toId = globals.PrimaryRoomPath[i + 1];
                if (!globals.Corridors.TryGetValue(CorridorKey.Create(fromId, toId), out var corridor))
                    continue;

                if (globals.Graph.IsBridgeEdge(fromId, toId, startId, targetId))
                {
                    return (corridor, fromId, toId);
                }
            }

            // Fallback: lock the corridor adjacent to the objective on the primary path so a locked
            // door is always placed. The player may bypass via a loop, but the gating contract holds.
            for (int i = globals.PrimaryRoomPath.Count - 2; i >= 0; i--)
            {
                var fromId = globals.PrimaryRoomPath[i];
                var toId = globals.PrimaryRoomPath[i + 1];
                if (globals.Corridors.TryGetValue(CorridorKey.Create(fromId, toId), out var corridor))
                {
                    return (corridor, fromId, toId);
                }
            }

            return null;
        }

        private void PlaceSecrets(World world, GeneratorContext context, GenerationGlobals globals)
        {
            var candidateCorridor = globals.Corridors.Values.FirstOrDefault(c => c.Primary && !c.IsVerticalConnector);
            if (candidateCorridor == null)
            {
                candidateCorridor = CreateFallbackSecretCorridor(globals, world);
                if (candidateCorridor == null)
                    return;
            }

            DungeonRoom secretRoom;
            WorldLocation entryLocation;

            if (candidateCorridor.IsSecret && globals.RoomLookup.ContainsKey(candidateCorridor.To.Id))
            {
                secretRoom = candidateCorridor.To;
                entryLocation = candidateCorridor.Path.First();
            }
            else
            {
                var rng = context.GetRandom("secret:placement");
                var entryIndex = Math.Max(1, candidateCorridor.Path.Count / 2);
                entryLocation = candidateCorridor.Path[entryIndex];

                secretRoom = BuildSecretRoom(world, context, candidateCorridor.From.Level, entryLocation, globals, rng);
                if (secretRoom == null)
                    return;

                var secretCorridor = new DungeonCorridor(candidateCorridor.From, secretRoom, new List<WorldLocation> { entryLocation, secretRoom.Center }, primary: false)
                {
                    IsSecret = true
                };

                globals.Graph.AddEdge(candidateCorridor.From.Id, secretRoom.Id);
                globals.Corridors[CorridorKey.Create(candidateCorridor.From.Id, secretRoom.Id)] = secretCorridor;
                candidateCorridor = secretCorridor;
            }

            var door = new SecretDoor();
            door.Set(entryLocation);
            world.AddEntity(door);

            var torch = new LightEntity();
            torch.Set(secretRoom.Center);
            torch.Set(new LightSource(0.8, 6));
            world.AddEntity(torch);

            globals.InteractiveArtifacts.SecretRoomId = secretRoom.Id;
            context.Metrics.AlternateSolutions++;
        }

        private DungeonCorridor? CreateFallbackSecretCorridor(GenerationGlobals globals, World world)
        {
            var anchorRoom = globals.Rooms.FirstOrDefault(r => !r.IsSecret);
            if (anchorRoom == null)
                return null;

            var entry = anchorRoom.Center;
            var directions = new[]
            {
                (dx: 1, dy: 0),
                (dx: -1, dy: 0),
                (dx: 0, dy: 1),
                (dx: 0, dy: -1)
            };

            foreach (var (dx, dy) in directions)
            {
                var wallLoc = entry.FromDelta(dx, dy, 0);
                var secretOrigin = wallLoc.FromDelta(dx, dy, 0);

                if (!IsWithinBounds(secretOrigin, globals.Context))
                    continue;

                var secretRect = new Rectangle(secretOrigin.X, secretOrigin.Y, 2, 2);
                if (!IsWithinBounds(new WorldLocation(secretRect.Right - 1, secretRect.Bottom - 1, anchorRoom.Level), globals.Context))
                    continue;

                var secretRoom = new DungeonRoom(globals.GetNextRoomId(), anchorRoom.Level, secretRect, RoomShape.Rectangle, isSecret: true);
                foreach (var tile in secretRoom.Tiles)
                {
                    world.SetTerrain("Indoors", tile);
                }

                world.SetTerrain("Indoors", wallLoc);

                globals.Rooms.Add(secretRoom);
                globals.RoomLookup[secretRoom.Id] = secretRoom;
                globals.Graph.EnsureNode(secretRoom.Id);

                var corridorPath = new List<WorldLocation> { wallLoc, secretRoom.Center };
                var corridor = new DungeonCorridor(anchorRoom, secretRoom, corridorPath, primary: false) { IsSecret = true };
                globals.Corridors[CorridorKey.Create(anchorRoom.Id, secretRoom.Id)] = corridor;
                globals.Graph.AddEdge(anchorRoom.Id, secretRoom.Id);
                return corridor;
            }

            return null;
        }

        private static bool IsWithinBounds(WorldLocation location, GeneratorContext context)
        {
            return location.X >= 0 && location.X < context.Width &&
                   location.Y >= 0 && location.Y < context.Height;
        }

        private DungeonRoom? BuildSecretRoom(World world, GeneratorContext context, int level, WorldLocation anchor, GenerationGlobals globals, Random rng)
        {
            const int MaxAttempts = 8;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var size = rng.Next(3, 5);
                int maxX = Math.Max(1, context.Width - size - 1);
                int maxY = Math.Max(1, context.Height - size - 1);
                var x = Math.Clamp(anchor.X + rng.Next(-size, size), 1, maxX);
                var y = Math.Clamp(anchor.Y + rng.Next(-size, size), 1, maxY);
                var rect = new Rectangle(x, y, size, size);

                // Prefer non-overlapping placement; on the final attempt accept overlap so tight
                // maps still get a secret (visually merges with an existing room — acceptable
                // for narrative payoff, and the validator only counts that secrets exist).
                bool allowOverlap = attempt == MaxAttempts - 1;
                if (!allowOverlap && !globals.CanPlace(rect, level))
                    continue;

                var room = new DungeonRoom(globals.GetNextRoomId(), level, rect, RoomShape.Rectangle, isSecret: true);
                foreach (var tile in room.Tiles)
                {
                    world.SetTerrain("Indoors", tile);
                }

                globals.Rooms.Add(room);
                globals.RoomLookup[room.Id] = room;
                globals.Graph.EnsureNode(room.Id);
                return room;
            }

            return null;
        }

        private void PlaceTrapsAndTools(World world, GeneratorContext context, GenerationGlobals globals)
        {
            // Eligible rooms, boss level (deepest) first — the historical single trap went there —
            // then shallower levels. Room Ids ascend with insertion order, so the first entry equals
            // the prior FirstOrDefault(!IsSecret && Level == Levels-1).
            var eligibleRooms = globals.Rooms
                .Where(r => !r.IsSecret && r.Level == context.Levels - 1)
                .OrderBy(r => r.Id)
                .Concat(globals.Rooms
                    .Where(r => !r.IsSecret && r.Level != context.Levels - 1)
                    .OrderByDescending(r => r.Level).ThenBy(r => r.Id))
                .ToList();

            if (eligibleRooms.Count == 0)
                return;

            // trapDensity (0..1) scales trap count by the number of eligible rooms; absent => exactly
            // one trap in the boss room (historical behavior). Trap placement draws no RNG, so a
            // higher count cannot perturb the determinism of any other pass.
            int trapCount = context.HasParam("trapDensity")
                ? Math.Max(1, (int)Math.Round(context.GetDoubleParam("trapDensity", 0, 0, 1) * eligibleRooms.Count))
                : 1;
            trapCount = Math.Min(trapCount, eligibleRooms.Count);

            for (int i = 0; i < trapCount; i++)
            {
                PlaceOneTrap(world, context, eligibleRooms[i]);
            }
        }

        private void PlaceOneTrap(World world, GeneratorContext context, DungeonRoom trapRoom)
        {
            var trapX = (trapRoom.Center.X + 1).ForceInRange(1, context.Width - 2);
            var trapY = trapRoom.Center.Y.ForceInRange(1, context.Height - 2);
            var trapLocation = new WorldLocation(trapX, trapY, trapRoom.Level);
            world.SetTerrain("Indoors", trapLocation);

            var pressurePlate = new PressurePlate();
            pressurePlate.Set(trapLocation);
            var plate = pressurePlate.Get<PressureSensitive>();
            var bombLocation = trapLocation.FromDelta(1, 0, 0);
            world.SetTerrain("Indoors", bombLocation);
            var bomb = new Bomb();
            bomb.Set(bombLocation);
            var explosion = bomb.Get<DelayedExplosion>();
            explosion.BlastRadius = 2;
            explosion.Strength = 3;
            explosion.DetonationSeconds = 3;
            plate.TargetEntityIds.Add(bomb.EntityId);

            world.AddEntity(pressurePlate);
            world.AddEntity(bomb);

            // Telegraph cue via torch
            var cue = new LightEntity();
            var cueLoc = trapLocation.FromDelta(0, -1, 0);
            world.SetTerrain("Indoors", cueLoc);
            cue.Set(cueLoc);
            cue.Set(new LightSource(0.5, 4));
            world.AddEntity(cue);

            context.Metrics.TrapsPlaced++;

            var toolLocX = (trapRoom.Center.X - 1).ForceInRange(1, context.Width - 2);
            var toolLocation = new WorldLocation(toolLocX, trapRoom.Center.Y, trapRoom.Level);
            var crowbar = new CrowbarItem();
            crowbar.Set(toolLocation);
            world.AddEntity(crowbar);
            context.Metrics.AlternateSolutions++;
        }

        private void ComputeMetrics(GeneratorContext context, GenerationGlobals globals)
        {
            context.Metrics.Rooms = globals.Rooms.Count(r => !r.IsSecret);
            context.Metrics.Corridors = globals.Corridors.Count;
            context.Metrics.SecretsPlaced = globals.Rooms.Count(r => r.IsSecret);
            context.Metrics.LockedDoors = globals.InteractiveArtifacts.LockedDoorLocation.IsNone ? 0 : 1;

            var graph = globals.Graph;
            var degrees = graph.GetDegrees();
            if (degrees.Count == 0)
                return;

            context.Metrics.BranchingFactor = degrees.Values.Average();
            context.Metrics.DeadEndCount = degrees.Values.Count(v => v <= 1);

            var edges = graph.EdgeCount;
            var nodes = graph.NodeCount;
            if (edges > 0 && nodes > 0)
            {
                var loops = edges - nodes + 1;
                context.Metrics.LoopRatio = Math.Max(0, (double)loops / edges);
            }

            // Approximate path length histogram using BFS from start
            if (context.StartLocation is WorldLocation start && !start.IsNone)
            {
                var distances = graph.ComputeRoomDistances(globals.RoomLookup, start);
                foreach (var kvp in distances)
                {
                    context.Metrics.IncrementPathLength(kvp.Value);
                }
            }
        }

        #endregion

        private static double Distance(WorldLocation a, WorldLocation b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        #region Helper Types

        private sealed class GenerationGlobals
        {
            private int _roomId = 1;

            public GenerationGlobals(GeneratorContext context)
            {
                Context = context;
            }

            public GeneratorContext Context { get; }
            public List<DungeonRoom> Rooms { get; } = new();
            public Dictionary<int, DungeonRoom> RoomLookup { get; } = new();
            public Dictionary<CorridorKey, DungeonCorridor> Corridors { get; } = new();
            public RoomGraph Graph { get; } = new();
            public InteractiveArtifacts InteractiveArtifacts { get; } = new();
            public int? StartRoomId { get; set; }
            public int? ObjectiveRoomId { get; set; }
            public List<int> PrimaryRoomPath { get; } = new();

            public int GetNextRoomId() => _roomId++;

            public bool CanPlace(Rectangle rect, int level)
            {
                return !Rooms.Any(r => r.Level == level && r.Bounds.IntersectsWith(Expand(rect, 1)));
            }

            private static Rectangle Expand(Rectangle rect, int amount)
            {
                return Rectangle.FromLTRB(
                    rect.Left - amount,
                    rect.Top - amount,
                    rect.Right + amount,
                    rect.Bottom + amount);
            }
        }

        private sealed class InteractiveArtifacts
        {
            public WorldLocation LockedDoorLocation { get; set; } = WorldLocation.None;
            public int SecretRoomId { get; set; }
        }

        private sealed class LayoutHelper
        {
            private readonly GeneratorContext _context;
            private readonly GenerationGlobals _globals;
            private readonly Random _rng;
            private int _shapeCursor;

            public LayoutHelper(GeneratorContext context, int level, GenerationGlobals globals, Random rng)
            {
                _context = context;
                Level = level;
                _globals = globals;
                _rng = rng;
            }

            public int Level { get; }
            public int Attempts { get; set; }
            public List<DungeonRoom> RoomsOnLevel { get; } = new();

            public RoomShape GetNextShape()
            {
                var allShapes = Enum.GetValues(typeof(RoomShape)).Cast<RoomShape>().Where(s => s != RoomShape.Secret).ToArray();
                var shape = allShapes[_shapeCursor % allShapes.Length];
                _shapeCursor++;
                if (_rng.NextDouble() < 0.25)
                {
                    shape = allShapes[_rng.Next(allShapes.Length)];
                }
                return shape;
            }

            public Rectangle GetRandomBounds(RoomShape shape)
            {
                int width = _rng.Next(5, 10);
                int height = _rng.Next(5, 10);

                if (shape == RoomShape.Circle)
                {
                    width = height = _rng.Next(5, 8);
                }

                int maxX = _context.Width - width - 2;
                int maxY = _context.Height - height - 2;
                if (maxX <= 2 || maxY <= 2)
                {
                    throw new InvalidOperationException(
                        $"Map ({_context.Width}x{_context.Height}) is too small to place a {shape} room of size {width}x{height}. " +
                        "AdvancedDungeonGenerator requires roughly 15x15 minimum to fit a single room with margin.");
                }

                int x = _rng.Next(2, maxX);
                int y = _rng.Next(2, maxY);
                return new Rectangle(x, y, width, height);
            }

            public bool CanPlace(Rectangle rect)
            {
                return _globals.CanPlace(rect, Level);
            }

            public DungeonRoom CreateRoom(World world, Rectangle bounds, RoomShape shape)
            {
                var room = new DungeonRoom(_globals.GetNextRoomId(), Level, bounds, shape, isSecret: false);
                foreach (var tile in room.Tiles)
                {
                    world.SetTerrain("Indoors", tile);
                }
                _globals.RoomLookup[room.Id] = room;
                _globals.Graph.EnsureNode(room.Id);
                _globals.Rooms.Add(room);
                RoomsOnLevel.Add(room);
                return room;
            }

            public void EnsureShapeVariety(World world)
            {
                var shapes = RoomsOnLevel.Select(r => r.Shape).Distinct().ToList();
                if (shapes.Count >= 2)
                    return;

                var fallbackShape = shapes.FirstOrDefault() == RoomShape.Rectangle ? RoomShape.L : RoomShape.Cross;
                var rect = GetRandomBounds(fallbackShape);
                if (!CanPlace(rect))
                    return;
                CreateRoom(world, rect, fallbackShape);
            }
        }

        private sealed record EdgeCandidate(DungeonRoom A, DungeonRoom B, double Weight);

        private sealed class DisjointSet
        {
            private readonly Dictionary<int, int> _parent = new();
            private readonly Dictionary<int, int> _rank = new();

            public DisjointSet(IEnumerable<int> nodes)
            {
                foreach (var node in nodes)
                {
                    _parent[node] = node;
                    _rank[node] = 0;
                }
            }

            public bool Union(int a, int b)
            {
                int rootA = Find(a);
                int rootB = Find(b);
                if (rootA == rootB)
                    return false;

                if (_rank[rootA] < _rank[rootB])
                    (rootA, rootB) = (rootB, rootA);

                _parent[rootB] = rootA;
                if (_rank[rootA] == _rank[rootB])
                    _rank[rootA]++;

                return true;
            }

            private int Find(int node)
            {
                if (_parent[node] != node)
                {
                    _parent[node] = Find(_parent[node]);
                }
                return _parent[node];
            }
        }

        private sealed class RoomGraph
        {
            // SortedSet gives deterministic BFS/DFS iteration order; HashSet iteration depends
            // on internal bucket state and produces non-deterministic neighbor traversal order.
            private readonly Dictionary<int, SortedSet<int>> _adjacency = new();

            public int EdgeCount => _adjacency.Sum(kvp => kvp.Value.Count) / 2;
            public int NodeCount => _adjacency.Count;

            public void EnsureNode(int id)
            {
                if (!_adjacency.ContainsKey(id))
                {
                    _adjacency[id] = new SortedSet<int>();
                }
            }

            public void AddEdge(int a, int b)
            {
                EnsureNode(a);
                EnsureNode(b);
                _adjacency[a].Add(b);
                _adjacency[b].Add(a);
            }

            public IReadOnlyDictionary<int, int> GetDegrees()
            {
                return _adjacency.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);
            }

            public bool IsBridgeEdge(int a, int b, int startId, int targetId)
            {
                if (!_adjacency.ContainsKey(a) || !_adjacency.ContainsKey(b))
                    return false;
                if (!_adjacency[a].Contains(b))
                    return false;

                var visited = new HashSet<int>();
                var queue = new Queue<int>();
                queue.Enqueue(startId);
                visited.Add(startId);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var neighbor in _adjacency[current])
                    {
                        if ((current == a && neighbor == b) || (current == b && neighbor == a))
                            continue;
                        if (visited.Add(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                return !visited.Contains(targetId);
            }

            public Dictionary<int, int> BfsDistances(int startId)
            {
                var dist = new Dictionary<int, int>();
                if (!_adjacency.ContainsKey(startId))
                    return dist;

                var queue = new Queue<int>();
                dist[startId] = 0;
                queue.Enqueue(startId);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var neighbor in _adjacency[current])
                    {
                        if (dist.ContainsKey(neighbor))
                            continue;
                        dist[neighbor] = dist[current] + 1;
                        queue.Enqueue(neighbor);
                    }
                }
                return dist;
            }

            public List<int> FindPath(int start, int target)
            {
                var queue = new Queue<int>();
                var prev = new Dictionary<int, int>();
                var visited = new HashSet<int>();

                queue.Enqueue(start);
                visited.Add(start);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current == target)
                        break;

                    foreach (var neighbor in _adjacency[current])
                    {
                        if (visited.Add(neighbor))
                        {
                            prev[neighbor] = current;
                            queue.Enqueue(neighbor);
                        }
                    }
                }

                if (!visited.Contains(target))
                    return new List<int>();

                var path = new List<int> { target };
                var node = target;
                while (prev.TryGetValue(node, out var parent))
                {
                    path.Add(parent);
                    node = parent;
                }
                path.Reverse();
                return path;
            }

            public Dictionary<int, int> ComputeRoomDistances(Dictionary<int, DungeonRoom> rooms, WorldLocation start)
            {
                var startRoom = rooms.Values.FirstOrDefault(r => r.Center == start);
                if (startRoom == null)
                    return new Dictionary<int, int>();

                var queue = new Queue<int>();
                var dist = new Dictionary<int, int> { [startRoom.Id] = 0 };
                queue.Enqueue(startRoom.Id);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var neighbor in _adjacency[current])
                    {
                        if (dist.ContainsKey(neighbor))
                            continue;
                        dist[neighbor] = dist[current] + 1;
                        queue.Enqueue(neighbor);
                    }
                }
                return dist;
            }
        }

        private sealed record CorridorKey(int A, int B)
        {
            public static CorridorKey Create(int a, int b)
            {
                if (a < b)
                    return new CorridorKey(a, b);
                return new CorridorKey(b, a);
            }
        }

        private sealed class DungeonCorridor
        {
            public DungeonCorridor(DungeonRoom from, DungeonRoom to, List<WorldLocation> path, bool primary)
            {
                From = from;
                To = to;
                Path = path;
                Primary = primary;
            }

            public DungeonRoom From { get; }
            public DungeonRoom To { get; }
            public List<WorldLocation> Path { get; }
            public bool Primary { get; }
            public bool IsVerticalConnector { get; set; }
            public bool IsSecret { get; set; }
        }

        private sealed class DungeonRoom
        {
            public DungeonRoom(int id, int level, Rectangle bounds, RoomShape shape, bool isSecret)
            {
                Id = id;
                Level = level;
                Bounds = bounds;
                Shape = shape;
                IsSecret = isSecret;
                Tiles = GenerateTiles(bounds, level, shape, out var center);
                Center = center;
            }

            public int Id { get; }
            public int Level { get; }
            public Rectangle Bounds { get; }
            public RoomShape Shape { get; }
            public bool IsSecret { get; }
            public WorldLocation Center { get; }
            public List<WorldLocation> Tiles { get; }

            private static List<WorldLocation> GenerateTiles(Rectangle bounds, int level, RoomShape shape, out WorldLocation center)
            {
                var tiles = new List<WorldLocation>();
                int cx = bounds.Left + bounds.Width / 2;
                int cy = bounds.Top + bounds.Height / 2;

                for (int y = bounds.Top; y < bounds.Bottom; y++)
                {
                    for (int x = bounds.Left; x < bounds.Right; x++)
                    {
                        bool include = shape switch
                        {
                            RoomShape.Rectangle => true,
                            RoomShape.L => (x < cx || y < cy),
                            RoomShape.Cross => Math.Abs(x - cx) <= 1 || Math.Abs(y - cy) <= 1,
                            RoomShape.Circle => Math.Pow(x - cx, 2) + Math.Pow(y - cy, 2) <= Math.Pow(bounds.Width / 2.0, 2),
                            _ => true
                        };

                        if (include)
                        {
                            tiles.Add(new WorldLocation(x, y, level));
                        }
                    }
                }

                center = new WorldLocation(cx, cy, level);
                if (!tiles.Contains(center) && tiles.Count > 0)
                {
                    center = tiles[tiles.Count / 2];
                }

                return tiles;
            }
        }

        private enum RoomShape
        {
            Rectangle,
            L,
            Cross,
            Circle,
            Secret
        }

        #endregion
    }
}



