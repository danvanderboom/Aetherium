using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.WorldGen.Training;
using Aetherium.Server.WorldGen;

namespace Aetherium.Test.WorldGen.Training
{
    [TestFixture]
    public class CurriculumGeneratorTests
    {
        [Test]
        public void AutoCurriculumGenerator_GenerateNextStage_InitialStage_LowDifficulty()
        {
            // Arrange
            var generator = new AutoCurriculumGenerator();
            PerformanceAnalysis? previousAnalysis = null;

            // Act
            var stage = generator.GenerateNextStage(previousAnalysis, null);

            // Assert
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.Difficulty, Is.LessThanOrEqualTo(30));
            Assert.That(stage.Prerequisites, Is.Empty);
            Assert.That(stage.Parameters.ContainsKey("width"), Is.True);
            Assert.That(stage.Parameters.ContainsKey("height"), Is.True);
        }

        [Test]
        public void AutoCurriculumGenerator_GenerateNextStage_HighSuccessRate_IncreasesDifficulty()
        {
            // Arrange
            var generator = new AutoCurriculumGenerator();
            var previousAnalysis = new PerformanceAnalysis
            {
                TotalSteps = 25,
                SuccessRate = 0.85 // 85% success rate
            };
            var previousStage = new CurriculumStage
            {
                StageId = "stage1",
                Difficulty = 30
            };

            // Act
            var stage = generator.GenerateNextStage(previousAnalysis, previousStage);

            // Assert
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.Difficulty, Is.GreaterThan(30));
            Assert.That(stage.Difficulty, Is.LessThanOrEqualTo(40)); // Increased by up to 10
        }

        [Test]
        public void AutoCurriculumGenerator_GenerateNextStage_LowSuccessRate_DecreasesDifficulty()
        {
            // Arrange
            var generator = new AutoCurriculumGenerator();
            var previousAnalysis = new PerformanceAnalysis
            {
                TotalSteps = 25,
                SuccessRate = 0.30 // 30% success rate
            };
            var previousStage = new CurriculumStage
            {
                StageId = "stage1",
                Difficulty = 50
            };

            // Act
            var stage = generator.GenerateNextStage(previousAnalysis, previousStage);

            // Assert
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.Difficulty, Is.LessThan(50));
            Assert.That(stage.Difficulty, Is.GreaterThanOrEqualTo(40)); // Decreased by up to 10
        }

        [Test]
        public void AutoCurriculumGenerator_GenerateNextStage_NavigationWeakness_ReducesMapSize()
        {
            // Arrange
            var generator = new AutoCurriculumGenerator();
            var previousAnalysis = new PerformanceAnalysis
            {
                TotalSteps = 25,
                SuccessRate = 0.60,
                IdentifiedWeaknesses = new List<string> { "Low success rate for navigation actions" }
            };
            var previousStage = new CurriculumStage
            {
                StageId = "stage1",
                Difficulty = 40,
                Parameters = new Dictionary<string, object>
                {
                    ["width"] = "50",
                    ["height"] = "50",
                    ["roomCount"] = "20"
                }
            };

            // Act
            var stage = generator.GenerateNextStage(previousAnalysis, previousStage);

            // Assert
            Assert.That(stage.Parameters.ContainsKey("width"), Is.True);
            Assert.That(stage.Parameters.ContainsKey("height"), Is.True);
            
            // Map size should be reduced
            if (stage.Parameters.ContainsKey("width") && int.TryParse(stage.Parameters["width"].ToString(), out var width))
            {
                Assert.That(width, Is.LessThanOrEqualTo(50));
            }
        }

        [Test]
        public void CurriculumDefinition_LoadFromJson_ValidJson_Succeeds()
        {
            // Arrange
            var curriculumJson = @"{
                ""curriculumId"": ""test-curriculum"",
                ""name"": ""Test Curriculum"",
                ""description"": ""Test curriculum for unit tests"",
                ""version"": ""1.0"",
                ""categories"": [""test"", ""unit""],
                ""stages"": [
                    {
                        ""stageId"": ""stage1"",
                        ""name"": ""Stage 1"",
                        ""description"": ""First stage"",
                        ""difficulty"": 20,
                        ""prerequisites"": {},
                        ""parameters"": {
                            ""width"": ""30"",
                            ""height"": ""30""
                        },
                        ""completionCriteria"": {
                            ""minSuccessRate"": 0.7,
                            ""minSuccessfulCompletions"": 3
                        }
                    }
                ]
            }";

            // Act
            var curriculum = System.Text.Json.JsonSerializer.Deserialize<CurriculumDefinition>(curriculumJson);

            // Assert
            Assert.That(curriculum, Is.Not.Null);
            Assert.That(curriculum.CurriculumId, Is.EqualTo("test-curriculum"));
            Assert.That(curriculum.Stages, Is.Not.Empty);
            Assert.That(curriculum.Stages.Count, Is.EqualTo(1));
            Assert.That(curriculum.Stages[0].StageId, Is.EqualTo("stage1"));
        }

        [Test]
        public void CurriculumDefinition_InvalidJson_ThrowsException()
        {
            // Arrange
            var invalidJson = "{ invalid json }";

            // Act & Assert
            Assert.Throws<System.Text.Json.JsonException>(() =>
            {
                System.Text.Json.JsonSerializer.Deserialize<CurriculumDefinition>(invalidJson);
            });
        }

        [Test]
        public void WorldGenerationRequest_ApplyCurriculumStage_UpdatesParameters()
        {
            // Arrange
            var request = new WorldGenerationRequest
            {
                Width = 20,
                Height = 20,
                Levels = 1,
                Template = GenerationTemplate.Dungeon
            };
            var stage = new CurriculumStage
            {
                StageId = "stage1",
                Difficulty = 40,
                Parameters = new Dictionary<string, object>
                {
                    ["width"] = "40",
                    ["height"] = "40",
                    ["levels"] = "2",
                    ["trapDensity"] = "0.3",
                    ["enemyCount"] = "5"
                }
            };

            // Act
            request.ApplyCurriculumStage(stage);

            // Assert
            Assert.That(request.Width, Is.EqualTo(40));
            Assert.That(request.Height, Is.EqualTo(40));
            Assert.That(request.Levels, Is.EqualTo(2));
            Assert.That(request.IsTrainingMode, Is.True);
            Assert.That(request.Parameters.ContainsKey("trapDensity"), Is.True);
            Assert.That(request.Parameters.ContainsKey("enemyCount"), Is.True);
        }
    }
}

