using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleGame.Components;
using ConsoleGame.Core;
using ConsoleGame.WorldGen.Features;

namespace ConsoleGame.WorldGen.Generators.Outdoor
{
    /// <summary>
    /// Composite outdoor generator that layers noise-based heightmaps, rivers, roads,
    /// settlements, and points of interest with deterministic metrics.
    /// </summary>
    public sealed class AdvancedOutdoorGenerator : IMapGenerator
    {
        private readonly PerlinTerrainGenerator _terrain = new PerlinTerrainGenerator();
        private readonly RiverCarverFeature _river = new RiverCarverFeature(width: 3, connectEdges: true);

        public World Generate(GeneratorContext context)
        {
            var world = _terrain.Generate(context);
            context.Levels = Math.Max(1, context.Levels);

            if (!world.TileTypes.ContainsKey("Monster"))
            {
                world.TileTypes["Monster"] = new TileType
                {
                    Name = "Monster",
                    Settings = new Dictionary<string, string>
                    {
                        { "MapCharacter", "M" },
                        { "BackgroundColor", ConsoleColor.Black.ToString() },
                        { "ForegroundColor", ConsoleColor.DarkRed.ToString() }
                    }
                };
            }

            _river.Apply(world, context);

            var rng = context.GetRandom("outdoor:layout");
            var poiPlanner = new OutdoorPoiPlanner(world, context, rng);
            poiPlanner.CreateSettlements();
            poiPlanner.ConnectRoads();
            poiPlanner.PlacePointsOfInterest();

            context.StartLocation ??= poiPlanner.PrimarySettlement.Center;
            context.ObjectiveLocation = poiPlanner.FinalObjective;
            context.PrimaryPath.Clear();
            context.PrimaryPath.AddRange(poiPlanner.PrimaryRoute);

            ComputeBiomeCoverage(world, context);

            return world;
        }

        private static void ComputeBiomeCoverage(World world, GeneratorContext context)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int total = 0;
            foreach (var kvp in world.EntitiesByLocation)
            {
                var terrain = world.GetTerrain(kvp.Key);
                if (terrain == null)
                    continue;
                total++;
                var name = terrain.Type.Name;
                counts[name] = counts.TryGetValue(name, out var count) ? count + 1 : 1;
            }

            if (total == 0)
                return;

            foreach (var kvp in counts)
            {
                context.Metrics.RecordBiomeCoverage(kvp.Key, kvp.Value / (double)total);
            }
        }

        private sealed class OutdoorPoiPlanner
        {
            private readonly World _world;
            private readonly GeneratorContext _context;
            private readonly Random _rng;
            private readonly List<Settlement> _settlements = new();
            private readonly List<WorldLocation> _poi = new();

            public OutdoorPoiPlanner(World world, GeneratorContext context, Random rng)
            {
                _world = world;
                _context = context;
                _rng = rng;
            }

            public Settlement PrimarySettlement => _settlements.First();
            public WorldLocation FinalObjective => _poi.Last();
            public List<WorldLocation> PrimaryRoute { get; } = new();

            public void CreateSettlements()
            {
                var center = new WorldLocation(_context.Width / 2, _context.Height / 2, _context.ZLevel);
                var capital = BuildSettlement(center, size: 8, name: "Capital City");
                _settlements.Add(capital);

                for (int i = 0; i < 2; i++)
                {
                    var candidate = new WorldLocation(
                        _rng.Next(6, _context.Width - 6),
                        _rng.Next(6, _context.Height - 6),
                        _context.ZLevel);
                    var village = BuildSettlement(candidate, size: 5, name: $"Village-{i + 1}");
                    _settlements.Add(village);
                }
            }

            public void ConnectRoads()
            {
                PrimaryRoute.Clear();
                var capital = _settlements.First();
            PrimaryRoute.Add(capital.Center);
                foreach (var settlement in _settlements.Skip(1))
                {
                    var path = CarveRoad(capital.Center, settlement.Center);
                    if (settlement == _settlements[1])
                    {
                        PrimaryRoute.AddRange(path);
                    }
                }
            PrimaryRoute.Add(_settlements[1].Center);
            }

            public void PlacePointsOfInterest()
            {
                // Dungeon entrance near last village
                var anchorSettlement = _settlements.Last();
                var dungeon = anchorSettlement.Center.FromDelta(10, 3, 0);
                _world.SetTerrain("Cave", dungeon);
                _poi.Add(dungeon);
                PrimaryRoute.AddRange(CarveRoad(_settlements[1].Center, dungeon));

                // Forest shrine near river (search for water tile)
                var shrine = FindTerrainNear("Water", radius: 6) ?? dungeon.FromDelta(-5, -5, 0);
                _world.SetTerrain("Indoors", shrine);
                _poi.Add(shrine);
            }

            private Settlement BuildSettlement(WorldLocation center, int size, string name)
            {
                for (int y = -size; y <= size; y++)
                {
                    for (int x = -size; x <= size; x++)
                    {
                        var loc = center.FromDelta(x, y, 0);
                        if (Math.Abs(x) == size || Math.Abs(y) == size)
                        {
                            _world.SetTerrain("Road", loc);
                        }
                        else
                        {
                            _world.SetTerrain("Indoors", loc);
                        }
                    }
                }

                return new Settlement(name, center, size);
            }

            private List<WorldLocation> CarveRoad(WorldLocation start, WorldLocation end)
            {
                var path = new List<WorldLocation>();
                int x = start.X;
                int y = start.Y;

                while (x != end.X)
                {
                    x += x < end.X ? 1 : -1;
                    var loc = new WorldLocation(x, y, start.Z);
                    _world.SetTerrain("Road", loc);
                    path.Add(loc);
                }

                while (y != end.Y)
                {
                    y += y < end.Y ? 1 : -1;
                    var loc = new WorldLocation(x, y, start.Z);
                    _world.SetTerrain("Road", loc);
                    path.Add(loc);
                }

                return path;
            }

            private WorldLocation? FindTerrainNear(string terrainName, int radius)
            {
                foreach (var kvp in _world.EntitiesByLocation)
                {
                    var loc = kvp.Key;
                    var terrain = _world.GetTerrain(loc);
                    if (terrain == null || !string.Equals(terrain.Type.Name, terrainName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            var candidate = loc.FromDelta(dx, dy, 0);
                            if (!_world.EntitiesByLocation.ContainsKey(candidate))
                                continue;
                            if (_world.PassableTerrain(candidate))
                                return candidate;
                        }
                    }
                }
                return null;
            }
        }

        private sealed record Settlement(string Name, WorldLocation Center, int Radius);
    }
}


