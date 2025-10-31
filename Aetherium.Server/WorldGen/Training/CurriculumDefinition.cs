using System;
using System.Collections.Generic;
using System.Linq;

namespace Aetherium.WorldGen.Training
{
    /// <summary>
    /// Complete definition of a training curriculum with multiple stages.
    /// </summary>
    public sealed class CurriculumDefinition
    {
        /// <summary>
        /// Unique identifier for this curriculum.
        /// </summary>
        public string CurriculumId { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the curriculum.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the curriculum's purpose.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Category/tags for organizing curricula.
        /// </summary>
        public List<string> Categories { get; set; } = new List<string>();

        /// <summary>
        /// Version of the curriculum format.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Ordered list of stages in this curriculum.
        /// </summary>
        public List<CurriculumStage> Stages { get; set; } = new List<CurriculumStage>();

        /// <summary>
        /// Whether this curriculum uses automatic progression.
        /// </summary>
        public bool AutoProgression { get; set; } = false;

        /// <summary>
        /// Gets a stage by its ID.
        /// </summary>
        public CurriculumStage? GetStage(string stageId)
        {
            return Stages.FirstOrDefault(s => s.StageId == stageId);
        }

        /// <summary>
        /// Validates the curriculum structure.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(CurriculumId))
            {
                errors.Add("CurriculumId is required");
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add("Name is required");
            }

            if (Stages == null || Stages.Count == 0)
            {
                errors.Add("At least one stage is required");
            }
            else
            {
                var stageIds = new HashSet<string>();
                foreach (var stage in Stages)
                {
                    if (string.IsNullOrWhiteSpace(stage.StageId))
                    {
                        errors.Add("All stages must have a StageId");
                    }
                    else if (stageIds.Contains(stage.StageId))
                    {
                        errors.Add($"Duplicate StageId: {stage.StageId}");
                    }
                    else
                    {
                        stageIds.Add(stage.StageId);
                    }

                    // Validate prerequisites
                    if (stage.Prerequisites.RequiredStageIds != null)
                    {
                        foreach (var requiredStageId in stage.Prerequisites.RequiredStageIds)
                        {
                            if (!stageIds.Contains(requiredStageId) && !Stages.Any(s => s.StageId == requiredStageId))
                            {
                                errors.Add($"Stage {stage.StageId} requires unknown stage: {requiredStageId}");
                            }
                        }
                    }
                }
            }

            return errors;
        }
    }
}

