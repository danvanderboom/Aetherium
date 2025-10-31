//using System;
//using System.Linq;
//using System.Collections.Generic;
//using System.Collections.Concurrent;
//using System.Drawing;
//using System.Threading;
//using System.Threading.Tasks;
//using Aetherium;
//using Aetherium.Components;

//namespace Aetherium.Core
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

//        public Monster SelectRandomMonster()
//        {
//            var monsters = Entities.OfType<Monster>().ToList();
//            if (monsters.Count == 0)
//                return null;

//            if (monsters.Count == 1)
//                return monsters.First();

//            return monsters[rand.Next(0, monsters.Count)];
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

//    }
//}
