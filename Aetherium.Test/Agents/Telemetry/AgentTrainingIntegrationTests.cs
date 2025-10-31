using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Aetherium.Server.Agents.Telemetry;
using Aetherium.Server.WorldGen.Training;
using Aetherium.Server.WorldGen;

namespace Aetherium.Test.Agents.Telemetry
{
    [TestFixture]
    public class AgentTrainingIntegrationTests
    {
        [Test]
        public void PerformanceAnalyzer_WithTelemetrySnapshots_GeneratesCompleteAnalysis()
        {
            // Arrange
            var analyzer = new PerformanceAnalyzer();
            var snapshots = new List<PerformanceSnapshot>
            {
                new PerformanceSnapshot { AgentId = "test-agent", SessionId = "session1", ActionType = "move", ActionSucceeded = true, DecisionLatencyMs = 100, PerceptionComplexity = 10, Timestamp = DateTime.UtcNow },
                new PerformanceSnapshot { AgentId = "test-agent", SessionId = "session1", ActionType = "move", ActionSucceeded = true, DecisionLatencyMs = 120, PerceptionComplexity = 12, Timestamp = DateTime.UtcNow },
                new PerformanceSnapshot { AgentId = "test-agent", SessionId = "session1", ActionType = "pickup", ActionSucceeded = false, DecisionLatencyMs = 150, PerceptionComplexity = 15, Timestamp = DateTime.UtcNow },
                new PerformanceSnapshot { AgentId = "test-agent", SessionId = "session1", ActionType = "move", ActionSucceeded = true, DecisionLatencyMs = 110, PerceptionComplexity = 11, Timestamp = DateTime.UtcNow }
            };

            // Act
            var analysis = analyzer.Analyze(snapshots);

            // Assert
            Assert.That(analysis, Is.Not.Null);
            Assert.That(analysis.AgentId, Is.EqualTo("test-agent"));
            Assert.That(analysis.TotalSteps, Is.EqualTo(4));
            Assert.That(analysis.SuccessRate, Is.EqualTo(0.75));
            Assert.That(analysis.ActionTypeStats.Count, Is.EqualTo(2));
            Assert.That(analysis.ActionTypeStats.ContainsKey("move"), Is.True);
            Assert.That(analysis.ActionTypeStats.ContainsKey("pickup"), Is.True);
        }

        [Test]
        public void CurriculumStage_AppliedToWorldGenerationRequest_UpdatesCorrectly()
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
                StageId = "test-stage",
                Name = "Test Stage",
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
            Assert.That(request.Parameters["trapDensity"], Is.EqualTo("0.3"));
            Assert.That(request.Parameters.ContainsKey("enemyCount"), Is.True);
            Assert.That(request.Parameters["enemyCount"], Is.EqualTo("5"));
        }

        [Test]
        public void AutoCurriculumGenerator_Progression_AdjustsDifficultyBasedOnPerformance()
        {
            // Arrange
            var generator = new AutoCurriculumGenerator();
            var stage1 = new CurriculumStage { StageId = "stage1", Difficulty = 30 };
            var analysis1 = new PerformanceAnalysis { TotalSteps = 25, SuccessRate = 0.85 }; // High success

            // Act - Generate stage 2 based on high success
            var stage2 = generator.GenerateNextStage(analysis1, stage1);

            // Assert
            Assert.That(stage2, Is.Not.Null);
            Assert.That(stage2.Difficulty, Is.GreaterThan(stage1.Difficulty));

            // Arrange - Low success performance
            var analysis2 = new PerformanceAnalysis { TotalSteps = 25, SuccessRate = 0.25 }; // Low success

            // Act - Generate stage 3 based on low success
            var stage3 = generator.GenerateNextStage(analysis2, stage2);

            // Assert
            Assert.That(stage3, Is.Not.Null);
            Assert.That(stage3.Difficulty, Is.LessThan(stage2.Difficulty));
        }

        [Test]
        public void BenchmarkGenerator_FromRecipe_CreatesValidWorldGenerationRequest()
        {
            // Arrange
            var generator = new BenchmarkGenerator();
            var recipe = new BenchmarkRecipe
            {
                Generator = "AdvancedDungeonGenerator",
                Template = GenerationTemplate.Dungeon,
                Seed = 12345,
                GeneratorVersion = "1.0",
                Width = 40,
                Height = 40,
                Levels = 1,
                Parameters = new Dictionary<string, object>
                {
                    ["roomCount"] = "10",
                    ["trapDensity"] = "0.2",
                    ["enemyCount"] = "3"
                }
            };

            // Act
            var request = generator.GenerateRequest(recipe);

            // Assert
            Assert.That(request, Is.Not.Null);
            Assert.That(request.Width, Is.EqualTo(40));
            Assert.That(request.Height, Is.EqualTo(40));
            Assert.That(request.Levels, Is.EqualTo(1));
            Assert.That(request.Template, Is.EqualTo(GenerationTemplate.Dungeon));
            Assert.That(request.IsTrainingMode, Is.True);
            Assert.That(request.Parameters.ContainsKey("roomCount"), Is.True);
            Assert.That(request.Parameters.ContainsKey("trapDensity"), Is.True);
            Assert.That(request.Parameters.ContainsKey("enemyCount"), Is.True);
        }

        [Test]
        public void BenchmarkGenerator_Variations_HaveUniqueSeeds()
        {
            // Arrange
            var generator = new BenchmarkGenerator();
            var baseBenchmark = new BenchmarkScenario
            {
                BenchmarkId = "base",
                Recipe = new BenchmarkRecipe
                {
                    Seed = 10000,
                    Width = 30,
                    Height = 30
                }
            };

            // Act
            var variations = generator.GenerateVariations(baseBenchmark, 5);

            // Assert
            Assert.That(variations, Is.Not.Null);
            Assert.That(variations.Count, Is.EqualTo(5));
            
            // All variations should have unique IDs and seeds
            var ids = variations.Select(v => v.BenchmarkId).ToList();
            var seeds = variations.Select(v => v.Recipe.Seed).ToList();
            
            Assert.That(ids.Distinct().Count(), Is.EqualTo(5));
            Assert.That(seeds.Distinct().Count(), Is.EqualTo(5));
            
            // All seeds should differ from base
            Assert.That(seeds.All(s => s != baseBenchmark.Recipe.Seed), Is.True);
        }

        [Test]
        public void ReplayData_StoresFailedRun_WithFullContext()
        {
            // Arrange
            var replay = new ReplayData
            {
                AgentId = "test-agent",
                SessionId = "session1",
                CreatedAt = DateTime.UtcNow,
                BenchmarkName = "test-benchmark",
                FailureReason = "Multiple consecutive failures",
                TotalSteps = 5,
                InitialWorldState = "{}",
                Steps = new List<ReplayStep>
                {
                    new ReplayStep { StepNumber = 1, ActionType = "move", ActionSummary = "move forward", Succeeded = true, Timestamp = DateTime.UtcNow, PerceptionJson = "{}" },
                    new ReplayStep { StepNumber = 2, ActionType = "move", ActionSummary = "move forward", Succeeded = false, Timestamp = DateTime.UtcNow, PerceptionJson = "{}" },
                    new ReplayStep { StepNumber = 3, ActionType = "pickup", ActionSummary = "pickup key", Succeeded = false, Timestamp = DateTime.UtcNow, PerceptionJson = "{}" },
                    new ReplayStep { StepNumber = 4, ActionType = "move", ActionSummary = "move forward", Succeeded = false, Timestamp = DateTime.UtcNow, PerceptionJson = "{}" }
                }
            };

            // Assert
            Assert.That(replay, Is.Not.Null);
            Assert.That(replay.AgentId, Is.EqualTo("test-agent"));
            Assert.That(replay.SessionId, Is.EqualTo("session1"));
            Assert.That(replay.TotalSteps, Is.EqualTo(5));
            Assert.That(replay.Steps.Count, Is.EqualTo(4));
            Assert.That(replay.Steps.Count(s => !s.Succeeded), Is.EqualTo(3));
            Assert.That(replay.FailureReason, Is.Not.Empty);
        }

        [Test]
        public void PerformanceAnalysis_WithTrendData_CalculatesCorrectly()
        {
            // Arrange
            var analyzer = new PerformanceAnalyzer();
            var snapshots = new List<PerformanceSnapshot>();
            
            // First half: high success rate
            for (int i = 0; i < 10; i++)
            {
                snapshots.Add(new PerformanceSnapshot
                {
                    ActionSucceeded = true,
                    ActionType = "move",
                    DecisionLatencyMs = 100,
                    PerceptionComplexity = 10,
                    Timestamp = DateTime.UtcNow.AddMinutes(-i)
                });
            }
            
            // Second half: low success rate (degrading trend)
            for (int i = 0; i < 10; i++)
            {
                snapshots.Add(new PerformanceSnapshot
                {
                    ActionSucceeded = i < 3, // 30% success rate
                    ActionType = "move",
                    DecisionLatencyMs = 150, // Also increasing latency
                    PerceptionComplexity = 15,
                    Timestamp = DateTime.UtcNow.AddMinutes(-i)
                });
            }

            // Act
            var analysis = analyzer.Analyze(snapshots);

            // Assert
            Assert.That(analysis.TrendMetrics, Is.Not.Empty);
            Assert.That(analysis.TrendMetrics.ContainsKey("SuccessRateTrend"), Is.True);
            Assert.That(analysis.TrendMetrics["SuccessRateTrend"], Is.LessThan(0)); // Degrading trend
            
            // First half should have 100% success, second half 30%
            Assert.That(analysis.Recommendations, Is.Not.Empty);
        }
    }
}

