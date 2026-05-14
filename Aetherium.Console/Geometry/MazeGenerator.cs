using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Core;
using Aetherium.Geometry;
using Aetherium.Components;

namespace Aetherium
{
    /// <summary>
    /// Recursive back-tracker maze generator driven by a grid coloring that partitions
    /// locations into rooms, walls, and pillars.
    /// <para>
    /// This type is intentionally kept in the <c>Aetherium</c> namespace (not
    /// <c>Aetherium.WorldGen</c>) because an identical copy exists in the Server project.
    /// TODO: move both copies to a shared assembly (Aetherium.Core or a new
    /// Aetherium.Geometry.Algorithms project) and delete the duplicate.
    /// </para>
    /// </summary>
    public class MazeGenerator
    {
        public IList<WorldLocation> AllLocations { get; set; }

        public List<WorldLocation> Rooms { get; set; }
        public List<WorldLocation> Walls { get; set; }
        public List<WorldLocation> Pillars { get; set; }
        public GridColoring<string> Coloring { get; set; }

        World World;

        WorldLocation? _CurrentLocation;
        WorldLocation? CurrentLocation
        {
            get => _CurrentLocation;
            set
            {
                _CurrentLocation = value;
                CurrentLocationSet?.Invoke(_CurrentLocation ?? WorldLocation.None);
            }
        }

        // Accepts an injected Random so the maze is reproducible from a given seed.
        // Falls back to a freshly-created Random (thread-local, random seed) when null.
        private readonly Random _rand;

        // HashSet for O(1) membership tests; the underlying List<> was O(n) per lookup.
        public HashSet<WorldLocation> Visited;

        public event Action<WorldLocation> SetRoom;
        public event Action<WorldLocation> SetPillar;
        public event Action<WorldLocation> SetWall;
        public event Action<WorldLocation> RemoveWall;

        public event Action<WorldLocation>? CurrentLocationSet;

        public MazeGenerator() : this((Random?)null) { }

        public MazeGenerator(Random? random)
        {
            _rand = random ?? new Random();
            World = new World();
            AllLocations = new List<WorldLocation>();
            Visited = new HashSet<WorldLocation>();
            Coloring = new GridColoring<string>(new string[1, 1]);
            Rooms = new List<WorldLocation>();
            Walls = new List<WorldLocation>();
            Pillars = new List<WorldLocation>();
            SetRoom = _ => { };
            SetPillar = _ => { };
            SetWall = _ => { };
            RemoveWall = _ => { };
        }

        public MazeGenerator(World world,
            IEnumerable<WorldLocation> locations,
            GridColoring<string> coloring,
            Func<string, MazeLocationType> colorMapping,
            Action<WorldLocation> setRoom,
            Action<WorldLocation> setPillar,
            Action<WorldLocation> setWall,
            Action<WorldLocation> removeWall,
            Random? random = null)
        {
            _rand = random ?? new Random();
            World = world;
            AllLocations = locations.ToList();
            Visited = new HashSet<WorldLocation>();
            Coloring = coloring;

            SetRoom = setRoom;
            SetPillar = setPillar;
            SetWall = setWall;
            RemoveWall = removeWall;

            Rooms = new List<WorldLocation>();
            Walls = new List<WorldLocation>();
            Pillars = new List<WorldLocation>();

            foreach (var location in locations)
            {
                var color = coloring.GetColor(location.X, location.Y);
                var locationType = colorMapping(color);

                if (locationType == MazeLocationType.Room)
                {
                    Rooms.Add(location);
                }
                else if (locationType == MazeLocationType.Wall)
                {
                    var cells = coloring.GetConnectedCells(location.X, location.Y, color)
                        .Select(loc => new WorldLocation(loc.X, loc.Y, location.Z))
                        .Where(loc => World.PassableTerrain(loc))
                        .ToList();

                    foreach (var cell in cells)
                    {
                        // WorldLocation is a struct; the previous `cell != null` check was always
                        // true and has been removed.
                        if (!Walls.Contains(cell))
                            Walls.Add(cell);
                    }
                }
                else if (!Pillars.Contains(location))
                {
                    Pillars.Add(location);
                }
            }
        }

        public void Build()
        {
            Visited = new HashSet<WorldLocation>();

            var walls = Walls.ToList();
            foreach (var wall in walls)
                SetWall(wall);

            foreach (var room in Rooms)
                SetRoom(room);

            foreach (var pillar in Pillars)
                SetPillar(pillar);

            CurrentLocation = Rooms.SelectRandom();
        }

        /// <summary>
        /// Advances the maze by one step. Returns <c>true</c> while work was done (i.e., the
        /// maze was extended or a new unvisited room was connected), or <c>false</c> when the
        /// algorithm has no more rooms to visit.
        /// </summary>
        public bool BuildNext()
        {
            if (CurrentLocation == null)
                return false;

            Visited.Add(CurrentLocation);

            var unvisitedNeighbors = GetNeighborRooms(CurrentLocation)
                .Where(kvp => !Visited.Contains(kvp.Key))
                .ToList();

            if (unvisitedNeighbors.Any())
            {
                var selectedNeighbor = unvisitedNeighbors[_rand.Next(0, unvisitedNeighbors.Count)];
                Connect(selectedNeighbor.Value);
                CurrentLocation = selectedNeighbor.Key;
                return true;
            }

            // No unvisited neighbors from current location — scan all locations for an unvisited
            // room that has at least one visited neighbor (Prim's extension).
            CurrentLocation = null;
            foreach (var loc in AllLocations)
            {
                var visitedNeighbors = GetNeighborRooms(loc)
                    .Where(kvp => Visited.Contains(kvp.Key))
                    .ToList();

                if (!Visited.Contains(loc) && visitedNeighbors.Any())
                {
                    CurrentLocation = loc;
                    var neighbor = visitedNeighbors[_rand.Next(0, visitedNeighbors.Count)];
                    Connect(neighbor.Value);
                    return true;
                }
            }

            // No remaining unvisited rooms reachable — algorithm is complete.
            return false;
        }

        public IList<(WorldDirection Direction, WorldLocation Location)> GetNeighborLocations(WorldLocation loc) =>
            new List<(WorldDirection Direction, WorldLocation Location)>
            {
                (WorldDirection.West, loc.FromDelta(-1, 0, 0)),
                (WorldDirection.East, loc.FromDelta(+1, 0, 0)),
                (WorldDirection.North, loc.FromDelta(0, -1, 0)),
                (WorldDirection.South, loc.FromDelta(0, +1, 0))
            }
            .Where(t => World.EntitiesByLocation.ContainsKey(t.Location) && AllLocations.Contains(t.Location))
            .ToList();

        public IDictionary<WorldLocation, MazeNeighbor> GetNeighborRooms(WorldLocation loc)
        {
            var neighborRooms = new Dictionary<WorldLocation, MazeNeighbor>();

            var neighbors = GetNeighborLocations(loc);

            foreach (var neighbor in neighbors)
            {
                var neighborCluster = Coloring.GetConnectedCells(neighbor.Location.X, neighbor.Location.Y)
                    .Select(c => new WorldLocation(c.X, c.Y, loc.Z))
                    .ToList();

                var adjacentRooms = neighborCluster.SelectMany(
                    c => GetNeighborLocations(new WorldLocation(c.X, c.Y, loc.Z)))
                        .Distinct()
                        .Where(n => Rooms.Contains(n.Location) && n.Location != CurrentLocation)
                        .ToList();

                foreach (var room in adjacentRooms)
                    if (!neighborRooms.ContainsKey(room.Location))
                        neighborRooms.Add(room.Location, new MazeNeighbor
                        {
                            Direction = neighbor.Direction,
                            Walls = neighborCluster
                        });
            }

            return neighborRooms;
        }

        public void Connect(MazeNeighbor neighbor)
        {
            foreach (var loc in neighbor.Walls)
                RemoveWall(loc);
        }
    }
}
