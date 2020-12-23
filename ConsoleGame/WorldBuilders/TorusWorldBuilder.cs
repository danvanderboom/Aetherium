using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using ConsoleGame.Core;
using ConsoleGame.Components;
using ConsoleGame.Geometry;
using ConsoleGame.WorldBuilders.Features;

namespace ConsoleGame.WorldBuilders
{
    public class TorusWorldBuilder : WorldBuilder
    {
        Random rand = new Random();

        MazeGenerator? mazeGenerator;

        public TorusWorldBuilder() : base()
        {
        }

        public bool BuildMazeStep() => mazeGenerator?.BuildNext() ?? false;

        public override World Build()
        {
            World = new World();

            World.AddTileTypes(TileTypes);
            World.AddTerrainTypes(CreateTerrainTypes(TileTypes));

            // toroid standing up like a wheel, with the very top layer above ground
            // the rest underground in mazes and caves

            var torusFeature = new WorldFeature
            {
                FeatureBuilder = (w, f) => new TorusFeatureBuilder(w, f),
                Settings = new Dictionary<string, string> 
                {
                    { "Name", "Torus of Doom" },
                    { "RadialSymmetryAxis", "Z" },
                },
                Chunk = new WorldChunk // this is ignored for first tests
                {
                    Location = new WorldLocation(x: -50, y: -50, z: -28),
                    Size = new Size3d(length: 100, width: 100, depth: 30)
                }
            };

            World.Features.Add(torusFeature);

            World.Build();


            var target = World.EntitiesByLocation.Keys
                .Where(loc => loc.Z == -1 && World.PassableTerrain(loc))
                .ToList();
            var dist = World.GetTerrainDistribution(target);

            var coloring202 = new GridColoring<string>(
                new string[,]
                {
                    { "Red", "Blue" },
                    { "Blue", "White" },
                });
            var color202map = new Func<string, MazeLocationType>(
                color => color switch
                {
                    "White" => MazeLocationType.Room,
                    "Blue" => MazeLocationType.Wall,
                    "Red" => MazeLocationType.Pillar,
                    _ => MazeLocationType.Pillar
                });

            var coloring303 = new GridColoring<string>(
                new string[,]
                {
                    { "White", "Yellow", "Yellow" },
                    { "Blue", "Blue", "Yellow" },
                    { "Blue", "Yellow", "Blue" }
                });
            var color303map = new Func<string, MazeLocationType>(
                color => color switch
                {
                    "White" => MazeLocationType.Room,
                    "Yellow" => MazeLocationType.Wall,
                    "Blue" => MazeLocationType.Wall,
                    _ => MazeLocationType.Pillar
                });

            CreateMaze(target,
                coloring: coloring303,
                colorMapping: color => color switch
                {
                    "White" => MazeLocationType.Room,
                    "Yellow" => MazeLocationType.Wall,
                    "Blue" => MazeLocationType.Wall,
                    _ => MazeLocationType.Pillar
                }
            );

            return World;
        }

        private void CreateMaze(IEnumerable<WorldLocation> firstUndergroundLevel,
            GridColoring<string> coloring,
            Func<string, MazeLocationType> colorMapping)
        {
            if (World == null)
                throw new InvalidOperationException("World is null");

            mazeGenerator = new MazeGenerator(World, firstUndergroundLevel, coloring,
                colorMapping,
                setRoom: loc => World.SetTerrain("Indoors", loc), 
                setPillar: loc => World.SetTerrain("Mountain", loc),
                setWall: loc => World.SetTerrain("Mountain", loc),
                removeWall: loc =>
                {
                    foreach (var wall in coloring.GetConnectedCells(loc.X, loc.Y))
                        World.SetTerrain("Indoors", loc);
                });

            mazeGenerator.CurrentLocationSet += MazeGenerator_CurrentLocationSet;

            mazeGenerator.Build();
        }

        public event Action<WorldLocation>? MazeLocationSet;

        private void MazeGenerator_CurrentLocationSet(WorldLocation location)
        {
            MazeLocationSet?.Invoke(location);
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
                Name = "Player",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "*" },
                    { "BackgroundColor", ConsoleColor.White.ToString() },
                    { "ForegroundColor", ConsoleColor.Blue.ToString() },
                }
            },
            new TileType
            {
                Name = "Monster",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "!" },
                    { "BackgroundColor", ConsoleColor.Red.ToString() },
                    { "ForegroundColor", ConsoleColor.Black.ToString() },
                }
            },
            new TileType
            {
                Name = "DeadMonster",
                Settings = new Dictionary<string, string>
                {
                    { "MapCharacter", "!" },
                    { "BackgroundColor", ConsoleColor.DarkRed.ToString() },
                    { "ForegroundColor", ConsoleColor.Black.ToString() },
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
            }
        };
    }
}
