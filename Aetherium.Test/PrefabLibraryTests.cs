using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.WorldGen.Prefabs;

namespace Aetherium.Test
{
    [TestFixture]
    public class PrefabLibraryTests
    {
        private string _testDirectory = null!;

        [SetUp]
        public void SetUp()
        {
            // Create a temporary directory for test prefabs
            _testDirectory = Path.Combine(Path.GetTempPath(), $"prefabs-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up the test directory
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        private PrefabTemplate CreateTestPrefab(string id, string name, string category)
        {
            var tiles = new PrefabTile[3, 3];
            tiles[0, 0] = new PrefabTile { TerrainType = "Floor" };
            tiles[1, 0] = new PrefabTile { TerrainType = "Floor" };
            tiles[2, 0] = new PrefabTile { TerrainType = "Wall" };
            tiles[0, 1] = new PrefabTile { TerrainType = "Floor" };
            tiles[1, 1] = new PrefabTile { TerrainType = "Floor" };
            tiles[2, 1] = new PrefabTile { TerrainType = "Wall" };
            tiles[0, 2] = new PrefabTile { TerrainType = "Wall" };
            tiles[1, 2] = new PrefabTile { TerrainType = "Wall" };
            tiles[2, 2] = new PrefabTile { TerrainType = "Wall" };

            return new PrefabTemplate
            {
                PrefabId = id,
                Name = name,
                Category = category,
                Width = 3,
                Height = 3,
                Tiles = tiles,
                Metadata = new Dictionary<string, string>
                {
                    ["author"] = "test",
                    ["version"] = "1.0"
                }
            };
        }

        private void WritePrefabToFile(PrefabTemplate prefab, string fileName)
        {
            var filePath = Path.Combine(_testDirectory, fileName);
            var json = JsonSerializer.Serialize(prefab, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(filePath, json);
        }

        [Test]
        public async Task PrefabLibrary_ShouldRegister_SinglePrefab()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            var prefab = CreateTestPrefab("test-house", "Test House", "Building");

            // Act
            await library.RegisterPrefabAsync(prefab);
            var retrieved = await library.GetPrefabAsync("test-house");

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.PrefabId, Is.EqualTo("test-house"));
            Assert.That(retrieved.Name, Is.EqualTo("Test House"));
            Assert.That(retrieved.Category, Is.EqualTo("Building"));
        }

        [Test]
        public async Task PrefabLibrary_ShouldReturn_NullForInvalidId()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            var prefab = CreateTestPrefab("test-house", "Test House", "Building");
            await library.RegisterPrefabAsync(prefab);

            // Act
            var retrieved = await library.GetPrefabAsync("nonexistent-id");

            // Assert
            Assert.That(retrieved, Is.Null);
        }

        [Test]
        public void PrefabLibrary_ShouldLoad_FromDirectory()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            var prefab1 = CreateTestPrefab("house-1", "House 1", "Building");
            var prefab2 = CreateTestPrefab("house-2", "House 2", "Building");
            var prefab3 = CreateTestPrefab("tree-1", "Tree 1", "Terrain");

            WritePrefabToFile(prefab1, "house-1.json");
            WritePrefabToFile(prefab2, "house-2.json");
            WritePrefabToFile(prefab3, "tree-1.json");

            // Act
            library.LoadFromDirectory(_testDirectory);

            // Assert
            Assert.That(library.Count, Is.EqualTo(3));
            var ids = library.ListPrefabIds();
            Assert.That(ids, Contains.Item("house-1"));
            Assert.That(ids, Contains.Item("house-2"));
            Assert.That(ids, Contains.Item("tree-1"));
        }

        [Test]
        public void PrefabLibrary_ShouldLoad_FromSubdirectories()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            var buildingsDir = Path.Combine(_testDirectory, "Buildings");
            var terrainDir = Path.Combine(_testDirectory, "Terrain");
            Directory.CreateDirectory(buildingsDir);
            Directory.CreateDirectory(terrainDir);

            var prefab1 = CreateTestPrefab("building-1", "Building 1", "Building");
            var prefab2 = CreateTestPrefab("terrain-1", "Terrain 1", "Terrain");

            File.WriteAllText(Path.Combine(buildingsDir, "building-1.json"), 
                JsonSerializer.Serialize(prefab1));
            File.WriteAllText(Path.Combine(terrainDir, "terrain-1.json"), 
                JsonSerializer.Serialize(prefab2));

            // Act
            library.LoadFromDirectory(_testDirectory);

            // Assert
            Assert.That(library.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task PrefabLibrary_ShouldSearch_ByCategory()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            await library.RegisterPrefabAsync(CreateTestPrefab("house-1", "House 1", "Building"));
            await library.RegisterPrefabAsync(CreateTestPrefab("house-2", "House 2", "Building"));
            await library.RegisterPrefabAsync(CreateTestPrefab("tree-1", "Tree 1", "Terrain"));

            // Act
            var buildings = library.SearchPrefabs(category: "Building");
            var terrain = library.SearchPrefabs(category: "Terrain");

            // Assert
            Assert.That(buildings.Count, Is.EqualTo(2));
            Assert.That(terrain.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task PrefabLibrary_ShouldSearch_ByTags()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            var prefab1 = CreateTestPrefab("house-1", "House 1", "Building");
            prefab1.Metadata["type"] = "residential";
            
            var prefab2 = CreateTestPrefab("shop-1", "Shop 1", "Building");
            prefab2.Metadata["type"] = "commercial";

            await library.RegisterPrefabAsync(prefab1);
            await library.RegisterPrefabAsync(prefab2);

            // Act
            var residential = library.SearchPrefabs(tags: new List<string> { "residential" });
            var commercial = library.SearchPrefabs(tags: new List<string> { "commercial" });

            // Assert
            Assert.That(residential.Count, Is.EqualTo(1));
            Assert.That(residential[0].PrefabId, Is.EqualTo("house-1"));
            Assert.That(commercial.Count, Is.EqualTo(1));
            Assert.That(commercial[0].PrefabId, Is.EqualTo("shop-1"));
        }

        [Test]
        public async Task PrefabLibrary_ShouldGet_ByCategory()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            await library.RegisterPrefabAsync(CreateTestPrefab("house-1", "House 1", "Building"));
            await library.RegisterPrefabAsync(CreateTestPrefab("house-2", "House 2", "Building"));
            await library.RegisterPrefabAsync(CreateTestPrefab("tree-1", "Tree 1", "Terrain"));

            // Act
            var buildings = library.GetByCategory("Building");

            // Assert
            Assert.That(buildings.Count, Is.EqualTo(2));
            Assert.That(buildings.All(p => p.Category == "Building"));
        }

        [Test]
        public async Task PrefabLibrary_ShouldList_AllPrefabIds()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            await library.RegisterPrefabAsync(CreateTestPrefab("prefab-1", "Prefab 1", "Test"));
            await library.RegisterPrefabAsync(CreateTestPrefab("prefab-2", "Prefab 2", "Test"));
            await library.RegisterPrefabAsync(CreateTestPrefab("prefab-3", "Prefab 3", "Test"));

            // Act
            var ids = library.ListPrefabIds();

            // Assert
            Assert.That(ids.Count, Is.EqualTo(3));
            Assert.That(ids, Contains.Item("prefab-1"));
            Assert.That(ids, Contains.Item("prefab-2"));
            Assert.That(ids, Contains.Item("prefab-3"));
        }

        [Test]
        public async Task PrefabLibrary_ShouldUpdate_ExistingPrefab()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            var prefab = CreateTestPrefab("house-1", "Original House", "Building");
            await library.RegisterPrefabAsync(prefab);

            // Act - Update the prefab
            prefab.Name = "Updated House";
            await library.RegisterPrefabAsync(prefab);

            var retrieved = await library.GetPrefabAsync("house-1");

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.Name, Is.EqualTo("Updated House"));
        }

        [Test]
        public void PrefabLibrary_ShouldThrow_WhenLoadingFromDirectoryInGrainMode()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: false);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                library.LoadFromDirectory(_testDirectory));
        }

        [Test]
        public void PrefabLibrary_ShouldHandle_InvalidJsonFiles()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            var invalidFile = Path.Combine(_testDirectory, "invalid.json");
            File.WriteAllText(invalidFile, "{ invalid json }");

            // Act - Should not throw, just log error
            Assert.DoesNotThrow(() => library.LoadFromDirectory(_testDirectory));

            // Assert
            Assert.That(library.Count, Is.EqualTo(0));
        }

        [Test]
        public void PrefabLibrary_ShouldHandle_NonexistentDirectory()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            var nonexistentDir = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}");

            // Act - Should not throw, just log
            Assert.DoesNotThrow(() => library.LoadFromDirectory(nonexistentDir));

            // Assert
            Assert.That(library.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task PrefabLibrary_ShouldPreserve_TileStructure()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            var prefab = CreateTestPrefab("house-1", "House 1", "Building");
            
            // Set specific tile with entity
            prefab.Tiles[1, 1] = new PrefabTile 
            { 
                TerrainType = "Floor", 
                EntityType = "Table",
                EntityConfig = new Dictionary<string, object> { ["color"] = "brown" }
            };

            // Act
            await library.RegisterPrefabAsync(prefab);
            var retrieved = await library.GetPrefabAsync("house-1");

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.Tiles.GetLength(0), Is.EqualTo(3));
            Assert.That(retrieved.Tiles.GetLength(1), Is.EqualTo(3));
            Assert.That(retrieved.Tiles[1, 1].EntityType, Is.EqualTo("Table"));
        }

        [Test]
        public async Task PrefabLibrary_ShouldThrow_OnEmptyPrefabId()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            var prefab = CreateTestPrefab("", "Empty ID", "Test");

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await library.RegisterPrefabAsync(prefab));
        }

        [Test]
        public void PrefabLibrary_ShouldReport_Count()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);

            // Act
            Assert.That(library.Count, Is.EqualTo(0));

            library.RegisterPrefabAsync(CreateTestPrefab("p1", "P1", "Test")).Wait();
            Assert.That(library.Count, Is.EqualTo(1));

            library.RegisterPrefabAsync(CreateTestPrefab("p2", "P2", "Test")).Wait();
            Assert.That(library.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task PrefabLibrary_ShouldHandle_LargePrefabs()
        {
            // Arrange
            var library = new PrefabLibrary(useFileStorage: true);
            var largePrefab = new PrefabTemplate
            {
                PrefabId = "large-building",
                Name = "Large Building",
                Category = "Building",
                Width = 10,
                Height = 10,
                Tiles = new PrefabTile[10, 10]
            };

            // Initialize all tiles
            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    largePrefab.Tiles[x, y] = new PrefabTile { TerrainType = "Floor" };
                }
            }

            // Act
            await library.RegisterPrefabAsync(largePrefab);
            var retrieved = await library.GetPrefabAsync("large-building");

            // Assert
            Assert.That(retrieved, Is.Not.Null);
            Assert.That(retrieved.Width, Is.EqualTo(10));
            Assert.That(retrieved.Height, Is.EqualTo(10));
        }
    }
}

