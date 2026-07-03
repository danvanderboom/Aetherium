using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.Audio;

namespace Aetherium.Test.Audio
{
    [TestFixture]
    public class JsonAudioProfileRepositoryTests
    {
        private string _testDirectory = null!;
        private string _testFilePath = null!;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), $"audio-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testDirectory);
            _testFilePath = Path.Combine(_testDirectory, "test-profiles.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Test]
        public async Task InitializeAsync_CreatesEmptyProfiles_WhenFileDoesNotExist()
        {
            // Arrange
            var repo = new JsonAudioProfileRepository(_testFilePath);

            // Act
            await repo.InitializeAsync();
            var profiles = await repo.GetAllProfilesAsync();

            // Assert
            Assert.That(profiles, Is.Not.Null);
            Assert.That(profiles.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task InitializeAsync_LoadsProfiles_WhenFileExists()
        {
            // Arrange
            var testProfiles = new[]
            {
                new BiomeAudioProfile { Id = "forest", Name = "Forest", FootstepMaterial = "grass" },
                new BiomeAudioProfile { Id = "dungeon", Name = "Dungeon", FootstepMaterial = "stone" }
            };

            // Dispose the stream before handing the file to the repository. The
            // repository used to run GC.Collect() in InitializeAsync purely to
            // finalize a stream this test leaked; don't reintroduce that.
            await using (var fs = new FileStream(_testFilePath, FileMode.Create))
            {
                await System.Text.Json.JsonSerializer.SerializeAsync(fs, testProfiles);
            }

            var repo = new JsonAudioProfileRepository(_testFilePath);

            // Act
            await repo.InitializeAsync();
            var loaded = await repo.GetAllProfilesAsync();

            // Assert
            Assert.That(loaded.Count, Is.EqualTo(2));
            Assert.That(loaded.First(p => p.Id == "forest").Name, Is.EqualTo("Forest"));
            Assert.That(loaded.First(p => p.Id == "dungeon").Name, Is.EqualTo("Dungeon"));
        }

        [Test]
        public async Task GetProfileAsync_ReturnsProfile_WhenExists()
        {
            // Arrange
            var profile = new BiomeAudioProfile { Id = "forest", Name = "Forest" };
            var repo = new JsonAudioProfileRepository(_testFilePath);
            await repo.InitializeAsync();
            await repo.SaveProfileAsync(profile);

            // Act
            var result = await repo.GetProfileAsync("forest");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Id, Is.EqualTo("forest"));
            Assert.That(result.Name, Is.EqualTo("Forest"));
        }

        [Test]
        public async Task GetProfileAsync_ReturnsNull_WhenNotExists()
        {
            // Arrange
            var repo = new JsonAudioProfileRepository(_testFilePath);
            await repo.InitializeAsync();

            // Act
            var result = await repo.GetProfileAsync("nonexistent");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task SaveProfileAsync_CreatesFile_WhenNotExists()
        {
            // Arrange
            var profile = new BiomeAudioProfile { Id = "forest", Name = "Forest" };
            var repo = new JsonAudioProfileRepository(_testFilePath);
            await repo.InitializeAsync();

            // Act
            await repo.SaveProfileAsync(profile);

            // Assert
            Assert.That(File.Exists(_testFilePath), Is.True);
            var loaded = await repo.GetProfileAsync("forest");
            Assert.That(loaded, Is.Not.Null);
        }

        [Test]
        public async Task SaveProfileAsync_UpdatesExisting_WhenProfileExists()
        {
            // Arrange
            var profile1 = new BiomeAudioProfile { Id = "forest", Name = "Forest" };
            var profile2 = new BiomeAudioProfile { Id = "forest", Name = "Updated Forest", FootstepMaterial = "dirt" };
            var repo = new JsonAudioProfileRepository(_testFilePath);
            await repo.InitializeAsync();
            await repo.SaveProfileAsync(profile1);

            // Act
            await repo.SaveProfileAsync(profile2);
            var result = await repo.GetProfileAsync("forest");

            // Assert
            Assert.That(result!.Name, Is.EqualTo("Updated Forest"));
            Assert.That(result.FootstepMaterial, Is.EqualTo("dirt"));
        }

        [Test]
        public async Task DeleteProfileAsync_RemovesProfile()
        {
            // Arrange
            var profile = new BiomeAudioProfile { Id = "forest", Name = "Forest" };
            var repo = new JsonAudioProfileRepository(_testFilePath);
            await repo.InitializeAsync();
            await repo.SaveProfileAsync(profile);

            // Act
            await repo.DeleteProfileAsync("forest");
            var result = await repo.GetProfileAsync("forest");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void SaveProfileAsync_Throws_WhenProfileIdIsEmpty()
        {
            // Arrange
            var profile = new BiomeAudioProfile { Id = "", Name = "Invalid" };
            var repo = new JsonAudioProfileRepository(_testFilePath);

            // Act & Assert
            Assert.ThrowsAsync<ArgumentException>(async () => await repo.SaveProfileAsync(profile));
        }
    }
}

