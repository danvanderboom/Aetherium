//using System;
//using System.Linq;
//using System.Collections.Generic;
//using System.Collections.Concurrent;
//using System.Drawing;
//using System.Threading;
//using System.Threading.Tasks;
//using ConsoleGame;
//using ConsoleGame.Components;

//namespace ConsoleGame.Core
//{
//    public class GameWorld
//    {
//        public Size3d WorldSize { get; protected set; }

//        public ConcurrentBag<Entity> Entities { get; protected set; }

//        public TerrainType GetTerrain(Location location) => 
//            Terrain[location.Z, location.Y, location.X];

//        public TerrainType SetTerrain(Location location, TerrainType terrainType) => 
//            Terrain[location.Z, location.Y, location.X] = terrainType;

//        public ConcurrentDictionary<Location, Entity> EntitiesByLocation { get; protected set; }

//        public Guid CharacterMoveTimestamp { get; protected set; } = Guid.NewGuid();

//        public event Action<Character> CharacterDied;

//        Random rand = new Random();

//        TerrainType[,,] Terrain;

//        Task MonsterHeartbeatTask = null;

//        int staircaseCount = 120;

//        List<Rectangle> mazeRectangles;

//        ConsoleColor mapFrameColor = ConsoleColor.Gray;

//        class BoxFrameCharacters
//        {
//            public static char Corner = '#';
//            public static char Horizontal = '-';
//            public static char Vertical = '|';
//        }

//        public GameWorld(int length, int width, int depth)
//        {
//            WorldSize = new Size3d(length, width, depth);

//            Terrain = new TerrainType[depth, length, width];

//            EntitiesByLocation = new ConcurrentDictionary<Location, Entity>();
//            Entities = new ConcurrentBag<Entity>();
//        }

//        public Character AddPlayer(string name)
//        {
//            var player = new Character();

//            player.Set(new Health { Level = 100, MaxLevel = 100 });
//            player.Set(SelectRandomPassableLocation());

//            Entities.Add(player);

//            return player;
//        }

//        public Monster AddMonster(string name)
//        {
//            var location = SelectRandomPassableLocation();

//            var monster = new Monster(this);

//            monster.Set(new Health { Level = 10, MaxLevel = 10 });
//            monster.Set(location);

//            Entities.Add(monster);

//            if (MonsterHeartbeatTask == null)
//                MonsterHeartbeatTask = Task.Run(MonsterHeartbeat);

//            return monster;
//        }
        
//        async Task MonsterHeartbeat()
//        {
//            while (true)
//            {
//                var monsters = Entities.OfType<Monster>().ToList();

//                foreach (var monster in monsters)
//                    if (monster.Get<Health>().Level > 0)
//                        monster.Heartbeat();

//                await Task.Delay(1000);
//            }
//        }

//        public Location SelectRandomPassableLocation(int? zlock = null)
//        {
//            while (true)
//            {
//                var x = rand.Next(0, WorldSize.Width);
//                var y = rand.Next(0, WorldSize.Length);
//                var z = zlock.HasValue ? zlock.Value : rand.Next(0, WorldSize.Depth);

//                var location = new Location(x, y, z);

//                // avoid locations with other characters
//                var charactersAtLocation = Entities.OfType<Character>().Any(c => c.Get<Location>() == location);
//                if (charactersAtLocation)
//                    continue; // try again

//                if (PassableTerrain(GetTerrain(location)))
//                    return location;
//            }
//        }

//        public Monster SelectRandomMonster()
//        {
//            var monsters = Entities.OfType<Monster>().ToList();
//            if (monsters.Count == 0)
//                return null;

//            if (monsters.Count == 1)
//                return monsters.First();

//            return monsters[rand.Next(0, monsters.Count)];
//        }

//        public bool TryMove(Character player, Direction direction)
//        {
//            switch (direction)
//            {
//                case Direction.North:
//                    return TryMove(player, player.Get<Location>().FromDelta(0, -1, 0));
//                case Direction.South:
//                    return TryMove(player, player.Get<Location>().FromDelta(0, +1, 0));
//                case Direction.West:
//                    return TryMove(player, player.Get<Location>().FromDelta(0, -1, 0));
//                case Direction.East:
//                    return TryMove(player, player.Get<Location>().FromDelta(0, +1, 0));
//                case Direction.Up:
//                    return TryMove(player, player.Get<Location>().FromDelta(0, 0, +1));
//                case Direction.Down:
//                    return TryMove(player, player.Get<Location>().FromDelta(0, 0, -1));
//                default:
//                    return false;
//            }
//        }

//        public bool TryMove(Character player, Location location)
//        {
//            if (location.X < 0 || location.X >= WorldSize.Length || location.Y < 0 || location.Y >= WorldSize.Width)
//                return false;

//            // stop players (including monsters) from existing in the same location
//            var character = Entities.OfType<Character>().FirstOrDefault(p => p.Get<Location>() == location);
//            if (character != null)
//            {
//                var health = character.Get<Health>();
//                health.Level--;

//                if (health.Level == 0)
//                    CharacterDied?.Invoke(character);

//                return false;
//            }

//            var currentLocation = player.Get<Location>();
//            var up = currentLocation.FromDelta(0, 0, +1);
//            var down = currentLocation.FromDelta(0, 0, -1);

//            if (location == up && GetTerrain(currentLocation) != TerrainType.Upstairs)
//                return false;
//            else if (location == down && GetTerrain(currentLocation) != TerrainType.Downstairs)
//                return false;

//            if (PassableTerrain(location))
//            {
//                player.Set(location);

//                CharacterMoveTimestamp = Guid.NewGuid();

//                return true;
//            }

//            return false;
//        }

//        public double Distance(Location start, Location end) => 
//            Math.Sqrt((end.X - start.X) + (end.Y - start.Y) + (end.Z - start.Z));

//        public bool PassableTerrain(Location location) => 
//            location.X >= 0 && location.X < WorldSize.Width
//            && location.Y >= 0 && location.Y < WorldSize.Length
//            && location.Z >= 0 && location.Z < WorldSize.Depth
//            && PassableTerrain(GetTerrain(location));

//        public bool PassableTerrain(TerrainType terrainType)
//        {
//            switch (terrainType)
//            {
//                case TerrainType.Indoors:
//                case TerrainType.Upstairs:
//                case TerrainType.Downstairs:
//                case TerrainType.Road:
//                case TerrainType.Plains:
//                case TerrainType.Forest:
//                case TerrainType.Cave:
//                    return true;
//                case TerrainType.None:
//                case TerrainType.Wall:
//                case TerrainType.Mountain:
//                case TerrainType.Water:
//                default:
//                    return false;
//            }
//        }

//        bool IntersectsExistingRectangle(Rectangle rectangle)
//        {
//            foreach (var mazeRectangle in mazeRectangles.Select(r => r.ToEnclosingRectangle()))
//                if (mazeRectangle.IntersectsWith(rectangle))
//                    return true;

//            return false;
//        }

//        public void GenerateMazeWorld(int z = 0, int density = 100)
//        {
//            mazeRectangles = new List<Rectangle>();

//            for (int i = 0; i < 1000; i++)
//            {
//                var location = new Point(rand.Next(WorldSize.Length), rand.Next(WorldSize.Width));

//                var width = rand.Next(5, 25);
//                if (width % 2 == 0)
//                    width++;

//                if (location.X + width >= WorldSize.Length)
//                    continue;

//                var height = rand.Next(5, 25);
//                if (height % 2 == 0)
//                    height++;

//                if (location.Y + height >= WorldSize.Width)
//                    continue;

//                var size = new Size(width, height);

//                var rectangle = new Rectangle(location, size);

//                if (!IntersectsExistingRectangle(rectangle))
//                    mazeRectangles.Add(rectangle);
//            }

//            for (int y = 0; y < WorldSize.Width; y++)
//                for (int x = 0; x < WorldSize.Length; x++)
//                    SetTerrain(new Location(x, y, z), TerrainType.None);

//            var cellsUpdated = 0;

//            foreach (var rectangle in mazeRectangles)
//                for (int y = rectangle.Y; y < rectangle.Y + rectangle.Height; y++)
//                    for (int x = rectangle.X; x < rectangle.X + rectangle.Width; x++)
//                    {
//                        SetTerrain(new Location(x, y, z), TerrainType.Indoors);
//                        cellsUpdated++;
//                    }
//        }

//        int ForceInRange(int value, int min, int max) => Math.Min(max, Math.Max(min, value));

//        public void GenerateMazeRectangles()
//        {

//        }

//        public void GenerateWorld()
//        {
//            var cellCount = Terrain.GetLength(0) * Terrain.GetLength(1);

//            CreateLakes(0.1, 20, 150);
//        }

//        public void CreateLakes(double waterPercent, int minLakeSize, int maxLakeSize)
//        {
//            var cellCount = Terrain.GetLength(0) * Terrain.GetLength(1);

//            var waterCellCount = (int)Math.Round(cellCount * waterPercent);
            
//            var remainingWaterCells = waterCellCount;
//            while (remainingWaterCells > 0)
//            {
//                var location = SelectRandomPassableLocation();
//                var lakeSize = rand.Next(minLakeSize, maxLakeSize + 1);

//                CreateLake(location, lakeSize);

//                remainingWaterCells -= lakeSize;
//            }
//        }

//        public void CreateLake(Location center, int lakeSize)
//        {
//            var remaining = lakeSize;

//            while (remaining > 0)
//            {
//                SetTerrain(center, TerrainType.Water);
//                remaining--;
//            }
//        }

//        public void GenerateDefaultTerrain()
//        {
//            var z = WorldSize.Depth - 1;

//            for (int y = 0; y < WorldSize.Length; y++)
//            {
//                for (int x = 0; x < WorldSize.Width; x++)
//                {
//                    var onEdge = 
//                        y == 0 || y == WorldSize.Length - 1       // top and bottom rows
//                        || x == 0 || x == WorldSize.Width - 1;    // left and right columns

//                    if (onEdge) 
//                    {
//                        // the world is enclosed in impassable mountains
//                        SetTerrain(new Location(x, y, z), TerrainType.Mountain);
//                    }
//                    else
//                    {
//                        var d = rand.NextDouble();

//                        var terrainType = TerrainType.Plains;
//                        if (d < 0.05)
//                            terrainType = TerrainType.Forest;
//                        else if (d < 0.07)
//                            terrainType = TerrainType.Water;

//                        // fill the rest with plains
//                        SetTerrain(new Location(x, y, z), terrainType);
//                    }
//                }
//            }

//            if (WorldSize.Depth > 1)
//            {
//                for (z = 0; z < WorldSize.Depth - 1; z++)
//                    GenerateMazeWorld(z, density: 1000);

//                for (int i = 0; i < staircaseCount; i++)
//                {
//                    Location start;
//                    Location end;

//                    while (true)
//                    {
//                        start = SelectRandomPassableLocation();
//                        var up = start.FromDelta(0, 0, -1);
//                        var down = start.FromDelta(0, 0, +1);

//                        var validLocations = new List<Location>();

//                        if (PassableTerrain(up))
//                            validLocations.Add(up);

//                        if (PassableTerrain(down))
//                            validLocations.Add(down);

//                        if (validLocations.Count == 0)
//                            continue;

//                        end = validLocations[rand.Next(0, validLocations.Count)];

//                        break;
//                    }

//                    SetTerrain(start, start.Z > end.Z ? TerrainType.Downstairs : TerrainType.Upstairs);
//                    SetTerrain(end, start.Z > end.Z ? TerrainType.Upstairs : TerrainType.Downstairs);
//                }
//            }
//        }

//        // TODO: cleanup: is this still needed?
//        void ConnectTwoLevels(Location location1, Location location2)
//        {
//            if (location1.X != location2.X || location1.Y != location2.Y)
//                throw new ArgumentException("X and Y must be the same and Z must be one different");

//            if (Math.Abs(location1.Z - location2.Z) != 1)
//                throw new ArgumentException("X and Y must be the same and Z must be one different");

//            var lowerLevel = Math.Min(location1.Z, location2.Z);


//        }

//        public void DrawMapFrame(Size size, Point locationOnScreen)
//        {
//            Console.SetCursorPosition(locationOnScreen.X, locationOnScreen.Y);

//            Console.ForegroundColor = mapFrameColor;

//            var hline = 
//                BoxFrameCharacters.Corner
//                + new string(BoxFrameCharacters.Horizontal, size.Width - 2)
//                + BoxFrameCharacters.Corner;

//            Console.Write(hline);

//            for (int y = 0; y < size.Height - 2; y++)
//            {
//                Console.CursorTop = locationOnScreen.Y + y + 1;

//                Console.CursorLeft = locationOnScreen.X;
//                Console.Write(BoxFrameCharacters.Vertical);

//                Console.CursorLeft = locationOnScreen.X + size.Width - 1;
//                Console.Write(BoxFrameCharacters.Vertical);
//            }

//            Console.CursorTop += 1;
//            Console.CursorLeft = locationOnScreen.X;

//            Console.Write(hline);
//        }

//        // Always display current location (x, y) in the center of the map viewport.
//        public void DrawMap(Size mapSize, Point locationOnScreenTopLeft, Location locationInWorldTopLeft)
//        {
//            var oldBackgroundColor = Console.BackgroundColor;
//            var oldForegroundColor = Console.ForegroundColor;

//            for (int y = 0; y < mapSize.Height; y++)
//            {
//                Console.SetCursorPosition(locationOnScreenTopLeft.X, locationOnScreenTopLeft.Y + y);

//                for (int x = 0; x < mapSize.Width; x++)
//                {
//                    var worldLocation = new Location(
//                        locationInWorldTopLeft.X + x, 
//                        locationInWorldTopLeft.Y + y,
//                        locationInWorldTopLeft.Z);

//                    var character = Entities.OfType<Character>()
//                        .FirstOrDefault(c => c.Get<Location>() == worldLocation);

//                    if (character != null)
//                    {
//                        var healthLevel = character.Get<Health>().Level;

//                        if (character is Monster && healthLevel > 0)
//                            DrawTile(TileType.Monster);
//                        else if (character is Monster && healthLevel <= 0)
//                            DrawTile(TileType.DeadMonster);
//                        else
//                            DrawTile(TileType.Player);
//                    }
//                    else if (worldLocation.X < 0 || worldLocation.X >= WorldSize.Length 
//                        || worldLocation.Y < 0 || worldLocation.Y >= WorldSize.Width
//                        || worldLocation.Z < 0 || worldLocation.Z >= WorldSize.Depth)
//                    {
//                        DrawTile(TileType.None);
//                    }
//                    else
//                    {
//                        DrawTerrain(GetTerrain(worldLocation));
//                    }
//                }
//            }

//            Console.BackgroundColor = oldBackgroundColor;
//            Console.ForegroundColor = oldForegroundColor;
//        }

//        public void DrawTerrain(TerrainType terrainType) => DrawTile(terrainType.ToTileType());

//        public void DrawTile(TileType tileType)
//        {
//            var terrainInfo = TerrainInfo(tileType);

//            Console.BackgroundColor = terrainInfo.BackgroundColor;
//            Console.ForegroundColor = terrainInfo.ForegroundColor;

//            Console.Write(terrainInfo.MapSymbol);
//        }

//        public (char MapSymbol, ConsoleColor BackgroundColor, ConsoleColor ForegroundColor) TerrainInfo(TileType terrain) => 
//            terrain switch
//            {
//                TileType.None => (' ', ConsoleColor.Black, ConsoleColor.Black),
//                TileType.Indoors => (' ', ConsoleColor.Gray, ConsoleColor.Black),
//                TileType.Wall => ('|', ConsoleColor.Gray, ConsoleColor.DarkRed),
//                TileType.Mountain => ('^', ConsoleColor.DarkGray, ConsoleColor.White),
//                TileType.Road => ('=', ConsoleColor.Black, ConsoleColor.White),
//                TileType.Plains => ('.', ConsoleColor.DarkYellow, ConsoleColor.Yellow),
//                TileType.Forest => ('t', ConsoleColor.Black, ConsoleColor.Green),
//                TileType.Water => ('~', ConsoleColor.Blue, ConsoleColor.White),
//                TileType.Cave => ('c', ConsoleColor.Black, ConsoleColor.DarkGray),
//                TileType.Player => ('*', ConsoleColor.White, ConsoleColor.Blue),
//                TileType.Monster => ('!', ConsoleColor.Red, ConsoleColor.Black),
//                TileType.DeadMonster => ('!', ConsoleColor.DarkRed, ConsoleColor.Black),
//                TileType.Upstairs => ('+', ConsoleColor.Gray, ConsoleColor.Yellow),
//                TileType.Downstairs => ('-', ConsoleColor.Gray, ConsoleColor.Yellow),
//                _ => throw new NotImplementedException()
//            };
//    }
//}