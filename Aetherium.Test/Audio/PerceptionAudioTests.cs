using System;
using System.Collections.Generic;
using NUnit.Framework;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Model;
using Aetherium.Server;
using Aetherium.Server.Perception;

namespace Aetherium.Test.Audio
{
    [TestFixture]
    public class PerceptionAudioTests
    {
        private World _world = null!;
        private PerceptionService _perceptionService = null!;
        private WorldLocation _playerLocation = null!;

        [SetUp]
        public void SetUp()
        {
            _world = new World();

            // Add terrain types
            var tileTypes = new List<TileType>
            {
                new TileType { Name = "Forest", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Dungeon", Settings = new Dictionary<string, string>() },
                new TileType { Name = "Wall", Settings = new Dictionary<string, string>() }
            };
            _world.AddTileTypes(tileTypes);

            var terrainTypes = new List<TerrainType>
            {
                new TerrainType { Name = "Forest", TileType = tileTypes[0] },
                new TerrainType { Name = "Dungeon", TileType = tileTypes[1] },
                new TerrainType { Name = "Wall", TileType = tileTypes[2] }
            };
            _world.AddTerrainTypes(terrainTypes);

            _perceptionService = new PerceptionService();
            _playerLocation = new WorldLocation(10, 10, 0);
        }

        [Test]
        public void ComputePerception_IncludesAudio_WhenTerrainExists()
        {
            // Arrange
            _world.SetTerrain("Forest", _playerLocation);

            // Act
            var perception = _perceptionService.ComputePerception(
                _world,
                _playerLocation,
                Aetherium.WorldDirection.North,
                new System.Drawing.Size(20, 20));

            // Assert
            Assert.That(perception.Audio, Is.Not.Null);
            Assert.That(perception.Audio!.Biome, Is.EqualTo("forest"));
            Assert.That(perception.Audio.FootstepMaterial, Is.EqualTo("grass"));
        }

        [Test]
        public void ComputePerception_AudioBiome_MatchesTerrain()
        {
            // Arrange
            _world.SetTerrain("Dungeon", _playerLocation);

            // Act
            var perception = _perceptionService.ComputePerception(
                _world,
                _playerLocation,
                Aetherium.WorldDirection.North,
                new System.Drawing.Size(20, 20));

            // Assert
            Assert.That(perception.Audio!.Biome, Is.EqualTo("dungeon"));
            Assert.That(perception.Audio.FootstepMaterial, Is.EqualTo("stone"));
        }

        [Test]
        public void ComputePerception_AudioDangerLevel_Zero_WhenNoHeatTracker()
        {
            // Arrange
            _world.SetTerrain("Forest", _playerLocation);

            // Act
            var perception = _perceptionService.ComputePerception(
                _world,
                _playerLocation,
                Aetherium.WorldDirection.North,
                new System.Drawing.Size(20, 20),
                LightingMode.Torch,
                VisionMode.Normal,
                null, // No heat tracker
                DateTime.UtcNow);

            // Assert
            Assert.That(perception.Audio!.DangerLevel, Is.EqualTo(0.0f));
        }

        [Test]
        public void ComputePerception_AudioDangerLevel_ReflectsHeat_WhenHeatTrackerProvided()
        {
            // Arrange
            _world.SetTerrain("Forest", _playerLocation);
            var heatTracker = new HeatTrailTracker();
            var character = new Character();
            character.Set(_playerLocation);
            character.Set(new Aetherium.Components.HeatSignature(0.5, TimeSpan.FromSeconds(10)));
            heatTracker.RecordEntityPosition(character, _playerLocation, DateTime.UtcNow);

            // Act
            var perception = _perceptionService.ComputePerception(
                _world,
                _playerLocation,
                Aetherium.WorldDirection.North,
                new System.Drawing.Size(20, 20),
                LightingMode.Torch,
                VisionMode.Normal,
                heatTracker,
                DateTime.UtcNow);

            // Assert
            Assert.That(perception.Audio!.DangerLevel, Is.GreaterThan(0.0f));
        }

        [Test]
        public void ComputePerception_AudioReverbPreset_Set()
        {
            // Arrange
            _world.SetTerrain("Forest", _playerLocation);

            // Act
            var perception = _perceptionService.ComputePerception(
                _world,
                _playerLocation,
                Aetherium.WorldDirection.North,
                new System.Drawing.Size(20, 20));

            // Assert
            Assert.That(perception.Audio!.ReverbPreset, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void ComputePerception_AudioOcclusion_Set()
        {
            // Arrange
            _world.SetTerrain("Forest", _playerLocation);

            // Act
            var perception = _perceptionService.ComputePerception(
                _world,
                _playerLocation,
                Aetherium.WorldDirection.North,
                new System.Drawing.Size(20, 20));

            // Assert
            Assert.That(perception.Audio!.Occlusion, Is.GreaterThanOrEqualTo(0.0f));
            Assert.That(perception.Audio.Occlusion, Is.LessThanOrEqualTo(1.0f));
        }

        [Test]
        public void ComputePerception_AudioSuggestedMusicTrack_Set()
        {
            // Arrange
            _world.SetTerrain("Forest", _playerLocation);

            // Act
            var perception = _perceptionService.ComputePerception(
                _world,
                _playerLocation,
                Aetherium.WorldDirection.North,
                new System.Drawing.Size(20, 20));

            // Assert
            Assert.That(perception.Audio!.SuggestedMusicTrack, Is.Not.Null.And.Not.Empty);
        }
    }
}

