using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using ConsoleGame.Core;
using ConsoleGame.Geometry;
using ConsoleGame.Components;

namespace ConsoleGame
{
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

        Random rand = new Random();

        public List<WorldLocation> Visited;

        public event Action<WorldLocation> SetRoom;
        public event Action<WorldLocation> SetPillar;
        public event Action<WorldLocation> SetWall;
        public event Action<WorldLocation> RemoveWall;

        public event Action<WorldLocation>? CurrentLocationSet;

        public MazeGenerator()
        {
            World = new World();
            AllLocations = new List<WorldLocation>();
            Visited = new List<WorldLocation>();
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
            Action<WorldLocation> removeWall)
        {
            World = world;
            AllLocations = locations.ToList();
            Visited = new List<WorldLocation>();
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
                        if (cell != null && !Walls.Contains(cell))
                            Walls.Add(cell);
                }
                else if (!Pillars.Contains(location))
                {
                    Pillars.Add(location);
                }
            }
        }

        public void Build()
        {
            Visited = new List<WorldLocation>();

            var allLocationsCount = AllLocations.Count;

            var walls = Walls.ToList();
            foreach (var wall in walls)
                SetWall(wall);

            var wallCount = walls.Count;

            foreach (var room in Rooms)
                SetRoom(room);

            var roomCount = Rooms.Count;

            //var pillars = AllLocations.Except(Walls.Union(Rooms)).ToList();
            foreach (var pillar in Pillars)
                SetPillar(pillar);

            var pillarCount = Pillars.Count;

            var thingCount = wallCount + roomCount + pillarCount;
            var diff = allLocationsCount - thingCount;

            CurrentLocation = Rooms.SelectRandom();

            //while (BuildNext()) { }
        }

        public bool BuildNext()
        {
            if (CurrentLocation == null)
                return false;

            if (!Visited.Contains(CurrentLocation))
                Visited.Add(CurrentLocation);

            var neighbors = GetNeighborRooms(CurrentLocation);

            var unvisitedNeighbors = GetNeighborRooms(CurrentLocation)
                .Where(kvp => !Visited.Contains(kvp.Key))
                .ToList();

            if (unvisitedNeighbors.Any())
            {
                var selectedNeighbor = unvisitedNeighbors[rand.Next(0, unvisitedNeighbors.Count)];
                Connect(selectedNeighbor.Value);
                CurrentLocation = selectedNeighbor.Key;
            }
            else
            {
                CurrentLocation = null;

                foreach (var loc in AllLocations)
                {
                    var visitedNeighbors = GetNeighborRooms(loc)
                        .Where(kvp => Visited.Contains(kvp.Key))
                        .ToList();

                    if (!Visited.Contains(loc) && visitedNeighbors.Any())
                    {
                        CurrentLocation = loc;
                        var neighbor = visitedNeighbors[rand.Next(0, visitedNeighbors.Count)];
                        Connect(neighbor.Value);
                        break;
                    }
                }
            }

            return true;
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
