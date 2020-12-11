using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using ConsoleGame.Components;
using ConsoleGame;

namespace ConsoleGame.Core
{
    public class GameWorld
    {
        public List<GameWorldLayer> Layers;

        public Size3d WorldSize { get; protected set; }

        public ConcurrentBag<Entity> Entities { get; protected set; }

        public TerrainType[,,] Terrain { get; protected set; }

        public TerrainType GetTerrain(Location location) => Terrain[location.Z, location.Y, location.X];

        Random rand = new Random();

        Task MonsterHeartbeatTask = null;

        List<Rectangle> mazeRectangles;

        public Guid MonsterMoveTimestamp { get; protected set; } = Guid.NewGuid();

        public GameWorld(int width, int length, int depth)
        {
            WorldSize = new Size3d(length, width, depth);

            Terrain = new TerrainType[depth, length, width];

            Entities = new ConcurrentBag<Entity>();

            Layers = new List<GameWorldLayer>();
        }

        public Character AddPlayer(string name)
        {
            var location = SelectRandomPassableLocation();

            var player = new Character
            {
                //Name = name,
                //Location = location
            };

            player.Get<Health>().Level = 100;
            player.Get<Health>().MaxLevel = 100;

            player.Get<Location>().X = location.X;
            player.Get<Location>().Y = location.Y;
            player.Get<Location>().Z = location.Z;

            Entities.Add(player);

            return player;
        }

        public Monster AddMonster(string name)
        {
            var location = SelectRandomPassableLocation();

            var monster = new Monster(this)
            {
                //Name = name,
                //Location = SelectRandomPassableLocation()
            };

            monster.Get<Health>().Level = 10;
            monster.Get<Health>().MaxLevel = 10;

            monster.Get<Location>().X = location.X;
            monster.Get<Location>().Y = location.Y;
            monster.Get<Location>().Z = location.Z;

            Entities.Add(monster);

            if (MonsterHeartbeatTask == null)
                MonsterHeartbeatTask = Task.Run(MonsterHeartbeat);

            return monster;
        }
        
        async Task MonsterHeartbeat()
        {
            while (true)
            {
                var monsters = Entities.OfType<Monster>().ToList();

                foreach (var monster in monsters)
                    monster.Heartbeat();

                await Task.Delay(1000);
            }
        }

        public Location SelectRandomPassableLocation()
        {
            while (true)
            {
                var x = rand.Next(0, WorldSize.Length);
                var y = rand.Next(0, WorldSize.Width);
                var z = rand.Next(0, WorldSize.Depth);

                var location = new Location(x, y, z);

                var charactersAtLocation = Entities.OfType<Character>().Any(c => c.Get<Location>() == location);

                if (PassableTerrain(Terrain[z, y, x]))
                    return location;
            }
        }

        public Monster SelectRandomMonster()
        {
            var monsters = Entities.OfType<Monster>().ToList();
            if (monsters.Count == 0)
                return null;

            if (monsters.Count == 1)
                return monsters.First();

            return monsters[rand.Next(0, monsters.Count)];
        }

        public bool TryMove(Character player, Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                    return TryMove(player, player.Get<Location>().FromDelta(0, -1, 0));
                case Direction.South:
                    return TryMove(player, player.Get<Location>().FromDelta(0, +1, 0));
                case Direction.West:
                    return TryMove(player, player.Get<Location>().FromDelta(0, -1, 0));
                case Direction.East:
                    return TryMove(player, player.Get<Location>().FromDelta(0, +1, 0));
                default:
                    return false;
            }
        }

        public bool TryMove(Character player, Location location)
        {
            if (location.X < 0 || location.X >= WorldSize.Length || location.Y < 0 || location.Y >= WorldSize.Width)
                return false;

            // stop players (including monsters) from existing in the same location
            if (Entities.OfType<Character>().Any(p => p.Get<Location>() == location))
                return false;

            if (PassableTerrain(location))
            {
                if (player is Monster)
                    MonsterMoveTimestamp = Guid.NewGuid();

                player.Set(location);

                return true;
            }

            return false;
        }

        public double Distance(Location start, Location end) => 
            Math.Sqrt((end.X - start.X) + (end.Y - start.Y) + (end.Z - start.Z));

        public bool PassableTerrain(Location location) => 
            PassableTerrain(Terrain[location.Z, location.Y, location.X]);

        public bool PassableTerrain(TerrainType terrainType)
        {
            switch (terrainType)
            {
                case TerrainType.Indoors:
                case TerrainType.Road:
                case TerrainType.Plains:
                case TerrainType.Forest:
                case TerrainType.Cave:
                    return true;
                case TerrainType.None:
                case TerrainType.Wall:
                case TerrainType.Mountain:
                case TerrainType.Player:
                case TerrainType.Monster:
                case TerrainType.Water:
                default:
                    return false;
            }
        }


        bool IntersectsExistingRectangle(Rectangle rectangle)
        {
            foreach (var mazeRectangle in mazeRectangles.Select(r => r.ToEnclosingRectangle()))
                if (mazeRectangle.IntersectsWith(rectangle))
                    return true;

            return false;
        }

        public void GenerateMazeWorld()
        {
            mazeRectangles = new List<Rectangle>();

            for (int i = 0; i < 1000; i++)
            {
                var location = new Point(rand.Next(WorldSize.Length), rand.Next(WorldSize.Width));

                var width = rand.Next(5, 25);
                if (width % 2 == 0)
                    width++;

                if (location.X + width >= WorldSize.Length)
                    continue;

                var height = rand.Next(5, 25);
                if (height % 2 == 0)
                    height++;

                if (location.Y + height >= WorldSize.Width)
                    continue;

                var size = new Size(width, height);

                var rectangle = new Rectangle(location, size);

                if (!IntersectsExistingRectangle(rectangle))
                    mazeRectangles.Add(rectangle);
            }

            for (int z = 0; z < WorldSize.Depth; z++)
                for (int y = 0; y < WorldSize.Width; y++)
                    for (int x = 0; x < WorldSize.Length; x++)
                        Terrain[z, y, x] = TerrainType.None;

            var cellsUpdated = 0;

            foreach (var rectangle in mazeRectangles)
                for (int y = rectangle.Y; y < rectangle.Y + rectangle.Height; y++)
                    for (int x = rectangle.X; x < rectangle.X + rectangle.Width; x++)
                    {
                        Terrain[0, y, x] = TerrainType.Indoors;
                        cellsUpdated++;
                    }
        }

        int ForceInRange(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

        public void GenerateMazeRectangles()
        {

        }

        public void GenerateWorld()
        {
            var cellCount = Terrain.GetLength(0) * Terrain.GetLength(1);

            CreateLakes(0.1, 20, 150);
        }

        public void CreateLakes(double waterPercent, int minLakeSize, int maxLakeSize)
        {
            var cellCount = Terrain.GetLength(0) * Terrain.GetLength(1);

            var waterCellCount = (int)Math.Round(cellCount * waterPercent);
            
            var remainingWaterCells = waterCellCount;
            while (remainingWaterCells > 0)
            {
                var location = SelectRandomPassableLocation();
                var lakeSize = rand.Next(minLakeSize, maxLakeSize + 1);

                CreateLake(location, lakeSize);

                remainingWaterCells -= lakeSize;
            }
        }

        public void CreateLake(Location center, int lakeSize)
        {
            var remaining = lakeSize;

            while (remaining > 0)
            {
                Terrain[center.Z, center.Y, center.X] = TerrainType.Water;
                remaining--;
            }
        }

        public void GenerateDefaultTerrain()
        {
            for (int x = 0; x < WorldSize.Length; x++)
            {
                for (int y = 0; y < WorldSize.Width; y++)
                {
                    var onEdge = 
                        y == 0 || y == WorldSize.Width - 1       // top and bottom rows
                        || x == 0 || x == WorldSize.Length - 1;    // left and right columns

                    if (onEdge) 
                    {
                        // the world is enclosed in impassable mountains
                        Terrain[0, y, x] = TerrainType.Mountain;
                    }
                    else
                    {
                        var d = rand.NextDouble();

                        var t = TerrainType.Plains;
                        if (d < 0.05)
                            t = TerrainType.Forest;
                        else if (d < 0.07)
                            t = TerrainType.Water;

                        // fill the rest with plains
                        Terrain[0, y, x] = t;
                    }
                }
            }
        }

        class BoxFrameCharacters
        {
            public static char Corner = '#';
            public static char Horizontal = '-';
            public static char Vertical = '|';
        }

        public void DrawMapFrame(Size mapSize, Point locationOnScreenTopleft)
        {
            Console.SetCursorPosition(locationOnScreenTopleft.X - 1, locationOnScreenTopleft.Y - 1);

            var hline = 
                BoxFrameCharacters.Corner
                + new string(BoxFrameCharacters.Horizontal, mapSize.Width)
                + BoxFrameCharacters.Corner;

            Console.Write(hline);

            for (int y = 0; y < mapSize.Height; y++)
            {
                Console.CursorTop = locationOnScreenTopleft.Y + y;

                Console.CursorLeft = locationOnScreenTopleft.X - 1;
                Console.Write(BoxFrameCharacters.Vertical);

                Console.CursorLeft = locationOnScreenTopleft.X + mapSize.Width;
                Console.Write(BoxFrameCharacters.Vertical);
            }

            Console.CursorTop += 1;
            Console.CursorLeft = locationOnScreenTopleft.X - 1;

            Console.Write(hline);
        }

        // Always display current location (x, y) in the center of the map viewport.
        public void DrawMap(Size mapSize, Point locationOnScreenTopLeft, Location locationInWorldTopLeft)
        {
            var oldBackgroundColor = Console.BackgroundColor;
            var oldForegroundColor = Console.ForegroundColor;

            for (int y = 0; y < mapSize.Height; y++)
            {
                Console.SetCursorPosition(locationOnScreenTopLeft.X, locationOnScreenTopLeft.Y + y);

                for (int x = 0; x < mapSize.Width; x++)
                {
                    var worldLocation = new Location(
                        locationInWorldTopLeft.X + x, 
                        locationInWorldTopLeft.Y + y,
                        locationInWorldTopLeft.Z);

                    if (Entities.OfType<Character>().Any(c => c.Get<Location>() == worldLocation))
                    {
                        if (Entities.OfType<Character>().Any(c => c.Get<Location>() == worldLocation && c is Monster))
                            RenderTerrain(TerrainType.Monster);
                        else
                            RenderTerrain(TerrainType.Player);
                    }
                    else if (worldLocation.X < 0 || worldLocation.X >= WorldSize.Length 
                        || worldLocation.Y < 0 || worldLocation.Y >= WorldSize.Width
                        || worldLocation.Z < 0 || worldLocation.Z >= WorldSize.Depth)
                    {
                        RenderTerrain(TerrainType.None);
                    }
                    else
                    {
                        RenderTerrain(Terrain[worldLocation.Z, worldLocation.Y, worldLocation.X]);
                    }
                }
            }

            Console.BackgroundColor = oldBackgroundColor;
            Console.ForegroundColor = oldForegroundColor;
        }

        public void RenderTerrain(TerrainType terrain)
        {
            var terrainInfo = TerrainInfo(terrain);

            Console.BackgroundColor = terrainInfo.BackgroundColor;
            Console.ForegroundColor = terrainInfo.ForegroundColor;

            Console.Write(terrainInfo.MapSymbol);
        }

        public (char MapSymbol, ConsoleColor BackgroundColor, ConsoleColor ForegroundColor) TerrainInfo(TerrainType terrain) => 
            terrain switch
            {
                TerrainType.None => (' ', ConsoleColor.Black, ConsoleColor.Black),
                TerrainType.Indoors => (' ', ConsoleColor.Gray, ConsoleColor.Black),
                TerrainType.Wall => ('|', ConsoleColor.Gray, ConsoleColor.DarkRed),
                TerrainType.Mountain => ('^', ConsoleColor.DarkGray, ConsoleColor.White),
                TerrainType.Road => ('=', ConsoleColor.Black, ConsoleColor.White),
                TerrainType.Plains => ('.', ConsoleColor.DarkYellow, ConsoleColor.Yellow),
                TerrainType.Forest => ('t', ConsoleColor.Black, ConsoleColor.Green),
                TerrainType.Water => ('~', ConsoleColor.Blue, ConsoleColor.White),
                TerrainType.Cave => ('c', ConsoleColor.Black, ConsoleColor.DarkGray),
                TerrainType.Player => ('*', ConsoleColor.White, ConsoleColor.Blue),
                TerrainType.Monster => ('!', ConsoleColor.Red, ConsoleColor.Black),
                _ => throw new NotImplementedException()
            };
    }
}