using System.IO;
using Aetherium.Unity.Model;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Aetherium.Unity.Tests
{
    public class PerceptionParsingTests
    {
        [Test]
        public void ParsePerceptionJson_ValidFrame_DeserializesCorrectly()
        {
            // Arrange
            var jsonPath = Path.Combine(Application.streamingAssetsPath, "PerceptionFrames", "sample-frame.json");
            
            if (!File.Exists(jsonPath))
            {
                Assert.Fail($"Sample frame JSON not found at {jsonPath}");
                return;
            }

            var json = File.ReadAllText(jsonPath);

            // Act
            var perception = JsonConvert.DeserializeObject<PerceptionLite>(json);

            // Assert
            Assert.IsNotNull(perception, "Perception should not be null");
            Assert.IsNotNull(perception.PlayerLocation, "PlayerLocation should not be null");
            Assert.AreEqual(0, perception.PlayerLocation.X, "Player X should be 0");
            Assert.AreEqual(0, perception.PlayerLocation.Y, "Player Y should be 0");
            Assert.AreEqual(0, perception.PlayerLocation.Z, "Player Z should be 0");
            
            Assert.AreEqual(WorldDirectionLite.North, perception.PlayerHeading, "Player heading should be North");
            Assert.AreEqual(0, perception.HeadingDegrees, "Heading degrees should be 0");

            Assert.IsNotNull(perception.VisibleBounds, "VisibleBounds should not be null");
            Assert.AreEqual(-5, perception.VisibleBounds.X, "Bounds X should be -5");
            Assert.AreEqual(-5, perception.VisibleBounds.Y, "Bounds Y should be -5");
            Assert.AreEqual(11, perception.VisibleBounds.Width, "Bounds width should be 11");
            Assert.AreEqual(11, perception.VisibleBounds.Height, "Bounds height should be 11");

            Assert.IsNotNull(perception.Visuals, "Visuals should not be null");
            Assert.Greater(perception.Visuals.Count, 0, "Should have at least one visual");
        }

        [Test]
        public void ParsePerceptionJson_GridDimensions_MatchVisibleBounds()
        {
            // Arrange
            var jsonPath = Path.Combine(Application.streamingAssetsPath, "PerceptionFrames", "sample-frame.json");
            
            if (!File.Exists(jsonPath))
            {
                Assert.Fail($"Sample frame JSON not found at {jsonPath}");
                return;
            }

            var json = File.ReadAllText(jsonPath);
            var perception = JsonConvert.DeserializeObject<PerceptionLite>(json);

            // Act
            var bounds = perception.VisibleBounds;
            var expectedCellCount = bounds.Width * bounds.Height;

            // Assert
            // Visuals should be a subset of the bounds area
            Assert.LessOrEqual(perception.Visuals.Count, expectedCellCount, 
                $"Visual count ({perception.Visuals.Count}) should not exceed bounds area ({expectedCellCount})");
        }

        [Test]
        public void PerceptionLite_PlayerLocation_InitializedCorrectly()
        {
            // Arrange & Act
            var location = new WorldLocationLite(5, 10, 2);

            // Assert
            Assert.AreEqual(5, location.X);
            Assert.AreEqual(10, location.Y);
            Assert.AreEqual(2, location.Z);
        }

        [Test]
        public void PerceptionLite_WorldDirection_EnumValuesMatch()
        {
            // Act & Assert
            Assert.AreEqual(0, (int)WorldDirectionLite.North);
            Assert.AreEqual(1, (int)WorldDirectionLite.South);
            Assert.AreEqual(2, (int)WorldDirectionLite.East);
            Assert.AreEqual(3, (int)WorldDirectionLite.West);
        }

        [Test]
        public void PerceptionLite_RoundTrip_PreservesVisualsDictionary()
        {
            // Regression: Unity's JsonUtility silently drops both auto-property fields
            // and Dictionary<,> contents. Newtonsoft must preserve both.
            var original = new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(7, -3, 2),
                PlayerHeading = WorldDirectionLite.East,
                HeadingDegrees = 90,
                VisibleBounds = new RectangleLite(-5, -5, 11, 11),
                Visuals = new System.Collections.Generic.Dictionary<string, VisualLite>
                {
                    ["0,0,2"] = new VisualLite(new WorldLocationLite(0, 0, 2), "stone", 1.0),
                    ["1,0,2"] = new VisualLite(new WorldLocationLite(1, 0, 2), "grass", 0.5),
                },
                TileTypes = new System.Collections.Generic.Dictionary<string, TileTypeLite>(),
            };

            var json = JsonConvert.SerializeObject(original);
            var round = JsonConvert.DeserializeObject<PerceptionLite>(json);

            Assert.IsNotNull(round);
            Assert.AreEqual(7, round.PlayerLocation.X);
            Assert.AreEqual(-3, round.PlayerLocation.Y);
            Assert.AreEqual(2, round.PlayerLocation.Z);
            Assert.AreEqual(WorldDirectionLite.East, round.PlayerHeading);
            Assert.AreEqual(90, round.HeadingDegrees);

            Assert.AreEqual(2, round.Visuals.Count);
            Assert.IsTrue(round.Visuals.ContainsKey("1,0,2"));
            Assert.AreEqual("grass", round.Visuals["1,0,2"].TileTypeId);
            Assert.AreEqual(0.5, round.Visuals["1,0,2"].LightLevel);
        }

        [Test]
        public void PerceptionLite_RoundTrip_MutatingCloneDoesNotAffectOriginal()
        {
            // The mock provider raises PerceptionUpdated with a JSON-round-trip clone
            // so subscribers can safely snapshot. Verify the clone is genuinely deep.
            var original = new PerceptionLite
            {
                PlayerLocation = new WorldLocationLite(1, 1, 0),
                Visuals = new System.Collections.Generic.Dictionary<string, VisualLite>
                {
                    ["0,0,0"] = new VisualLite(new WorldLocationLite(0, 0, 0), "stone", 1.0),
                },
            };

            var json = JsonConvert.SerializeObject(original);
            var clone = JsonConvert.DeserializeObject<PerceptionLite>(json);
            Assert.IsNotNull(clone);

            clone!.PlayerLocation.X = 99;
            clone.Visuals["0,0,0"].LightLevel = 0.1;

            Assert.AreEqual(1, original.PlayerLocation.X, "Mutating clone leaked into original");
            Assert.AreEqual(1.0, original.Visuals["0,0,0"].LightLevel, "Mutating clone's nested visual leaked into original");
        }
    }
}

