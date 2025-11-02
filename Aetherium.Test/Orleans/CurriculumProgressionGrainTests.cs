using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Orleans;
using Orleans.Hosting;
using Orleans.TestingHost;
using Aetherium.WorldGen.Training;
using System.Reflection;

namespace Aetherium.Test.Orleans
{
    [TestFixture]
    public class CurriculumProgressionGrainTests
    {
        private TestCluster? _cluster;
        private CurriculumDefinition? _testCurriculum;

        [SetUp]
        public async Task SetUp()
        {
            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
            _cluster = builder.Build();
            await _cluster.DeployAsync();

            // Register a test curriculum
            _testCurriculum = new CurriculumDefinition
            {
                CurriculumId = "test-curriculum",
                Name = "Test Curriculum",
                Description = "Test curriculum for unit tests",
                Stages = new List<CurriculumStage>
                {
                    new CurriculumStage
                    {
                        StageId = "stage1",
                        Name = "Stage 1",
                        Difficulty = 20,
                        CompletionCriteria = new CompletionCriteria
                        {
                            MinSuccessfulCompletions = 2,
                            MinAttempts = 3
                        }
                    },
                    new CurriculumStage
                    {
                        StageId = "stage2",
                        Name = "Stage 2",
                        Difficulty = 40,
                        Prerequisites = new PrerequisiteRequirements
                        {
                            RequiredStageIds = new List<string> { "stage1" }
                        },
                        CompletionCriteria = new CompletionCriteria
                        {
                            MinSuccessfulCompletions = 2,
                            MinAttempts = 3
                        }
                    },
                    new CurriculumStage
                    {
                        StageId = "stage3",
                        Name = "Stage 3",
                        Difficulty = 60,
                        Prerequisites = new PrerequisiteRequirements
                        {
                            RequiredStageIds = new List<string> { "stage2" }
                        },
                        CompletionCriteria = new CompletionCriteria
                        {
                            MinSuccessfulCompletions = 2,
                            MinAttempts = 3
                        }
                    }
                }
            };

            // Register curriculum using reflection to access internal CurriculumLibrary
            var curriculumProgressionType = typeof(CurriculumProgressionGrain);
            var curriculumLibraryType = curriculumProgressionType.Assembly.GetTypes()
                .FirstOrDefault(t => t.Name == "CurriculumLibrary" && t.IsNestedPrivate);
            
            if (curriculumLibraryType != null)
            {
                var registerMethod = curriculumLibraryType.GetMethod("RegisterCurriculum", BindingFlags.Public | BindingFlags.Static);
                registerMethod?.Invoke(null, new object[] { _testCurriculum });
            }
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_cluster != null)
            {
                await _cluster.StopAllSilosAsync();
                _cluster.Dispose();
            }
        }

        [Test]
        public async Task CurriculumProgressionGrain_StartCurriculum_LoadsCurriculumAndSetsFirstStage()
        {
            // Arrange
            var agentId = "test-agent-1";
            var grain = _cluster!.GrainFactory.GetGrain<ICurriculumProgressionGrain>(agentId);

            // Act
            await grain.StartCurriculumAsync("test-curriculum", agentId);

            // Assert
            var currentStage = await grain.GetCurrentStageAsync();
            Assert.That(currentStage, Is.Not.Null);
            Assert.That(currentStage.StageId, Is.EqualTo("stage1"));
        }

        [Test]
        public async Task CurriculumProgressionGrain_RecordRun_TracksProgress()
        {
            // Arrange
            var agentId = "test-agent-2";
            var grain = _cluster!.GrainFactory.GetGrain<ICurriculumProgressionGrain>(agentId);
            await grain.StartCurriculumAsync("test-curriculum", agentId);

            // Act
            await grain.RecordRunAsync(successful: true, steps: 10);
            await grain.RecordRunAsync(successful: false, steps: 5);
            await grain.RecordRunAsync(successful: true, steps: 8);

            // Assert
            var progress = await grain.GetProgressAsync();
            Assert.That(progress, Is.Not.Null);
            Assert.That(progress.TotalRuns, Is.EqualTo(3));
            Assert.That(progress.SuccessfulRuns, Is.EqualTo(2));
            Assert.That(progress.CurrentStageId, Is.EqualTo("stage1"));
        }

        [Test]
        public async Task CurriculumProgressionGrain_TryAdvanceStage_WhenCriteriaMet_AdvancesToNextStage()
        {
            // Arrange
            var agentId = "test-agent-3";
            var grain = _cluster!.GrainFactory.GetGrain<ICurriculumProgressionGrain>(agentId);
            await grain.StartCurriculumAsync("test-curriculum", agentId);

            // Record enough successful runs to meet completion criteria (2 successful, 3 attempts)
            await grain.RecordRunAsync(successful: true, steps: 10);
            await grain.RecordRunAsync(successful: true, steps: 12);
            await grain.RecordRunAsync(successful: false, steps: 5); // 3rd attempt (doesn't need to be successful)

            // Act
            var advanced = await grain.TryAdvanceStageAsync();

            // Assert
            Assert.That(advanced, Is.True);
            var currentStage = await grain.GetCurrentStageAsync();
            Assert.That(currentStage, Is.Not.Null);
            Assert.That(currentStage.StageId, Is.EqualTo("stage2"));
        }

        [Test]
        public async Task CurriculumProgressionGrain_TryAdvanceStage_WhenCriteriaNotMet_DoesNotAdvance()
        {
            // Arrange
            var agentId = "test-agent-4";
            var grain = _cluster!.GrainFactory.GetGrain<ICurriculumProgressionGrain>(agentId);
            await grain.StartCurriculumAsync("test-curriculum", agentId);

            // Record insufficient runs (only 1 successful, needs 2)
            await grain.RecordRunAsync(successful: true, steps: 10);

            // Act
            var advanced = await grain.TryAdvanceStageAsync();

            // Assert
            Assert.That(advanced, Is.False);
            var currentStage = await grain.GetCurrentStageAsync();
            Assert.That(currentStage, Is.Not.Null);
            Assert.That(currentStage.StageId, Is.EqualTo("stage1"));
        }

        [Test]
        public async Task CurriculumProgressionGrain_GetProgress_ReturnsCompleteProgress()
        {
            // Arrange
            var agentId = "test-agent-5";
            var grain = _cluster!.GrainFactory.GetGrain<ICurriculumProgressionGrain>(agentId);
            await grain.StartCurriculumAsync("test-curriculum", agentId);

            await grain.RecordRunAsync(successful: true, steps: 10);
            await grain.RecordRunAsync(successful: true, steps: 12);
            await grain.RecordRunAsync(successful: false, steps: 5);

            // Act
            var progress = await grain.GetProgressAsync();

            // Assert
            Assert.That(progress, Is.Not.Null);
            Assert.That(progress.CurriculumId, Is.EqualTo("test-curriculum"));
            Assert.That(progress.TotalStages, Is.EqualTo(3));
            Assert.That(progress.TotalRuns, Is.EqualTo(3));
            Assert.That(progress.SuccessfulRuns, Is.EqualTo(2));
            Assert.That(progress.CurrentSuccessRate, Is.EqualTo(2.0 / 3.0).Within(0.001));
            Assert.That(progress.StageProgress, Is.Not.Empty);
        }

        [Test]
        public async Task CurriculumProgressionGrain_Reset_ClearsProgression()
        {
            // Arrange
            var agentId = "test-agent-6";
            var grain = _cluster!.GrainFactory.GetGrain<ICurriculumProgressionGrain>(agentId);
            await grain.StartCurriculumAsync("test-curriculum", agentId);
            await grain.RecordRunAsync(successful: true, steps: 10);

            // Act
            await grain.ResetAsync();

            // Assert
            var progress = await grain.GetProgressAsync();
            Assert.That(progress.TotalRuns, Is.EqualTo(0));
            Assert.That(progress.SuccessfulRuns, Is.EqualTo(0));
        }

        [Test]
        public async Task CurriculumProgressionGrain_GetNextTrainingStage_InAutoMode_ReturnsAutoGeneratedStage()
        {
            // Arrange
            var agentId = "test-agent-7";
            var grain = _cluster!.GrainFactory.GetGrain<ICurriculumProgressionGrain>(agentId);
            
            // Create an auto-progression curriculum
            var autoCurriculum = new CurriculumDefinition
            {
                CurriculumId = "auto-curriculum",
                Name = "Auto Curriculum",
                AutoProgression = true,
                Stages = new List<CurriculumStage>
                {
                    new CurriculumStage
                    {
                        StageId = "auto-stage1",
                        Name = "Auto Stage 1",
                        Difficulty = 20
                    }
                }
            };

            // Register auto curriculum
            var curriculumProgressionType = typeof(CurriculumProgressionGrain);
            var curriculumLibraryType = curriculumProgressionType.Assembly.GetTypes()
                .FirstOrDefault(t => t.Name == "CurriculumLibrary" && t.IsNestedPrivate);
            
            if (curriculumLibraryType != null)
            {
                var registerMethod = curriculumLibraryType.GetMethod("RegisterCurriculum", BindingFlags.Public | BindingFlags.Static);
                registerMethod?.Invoke(null, new object[] { autoCurriculum });
            }

            await grain.StartCurriculumAsync("auto-curriculum", agentId);

            // Act
            var nextStage = await grain.GetNextTrainingStageAsync();

            // Assert
            Assert.That(nextStage, Is.Not.Null);
        }

        private class TestSiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                siloBuilder.ConfigureApplicationParts(parts =>
                {
                    parts.AddApplicationPart(typeof(CurriculumProgressionGrain).Assembly).WithReferences();
                });
            }
        }
    }
}

