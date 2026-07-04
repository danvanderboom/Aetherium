using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Aetherium.WorldGen.Training;
using Aetherium.Model.Training;
using System.Text.Json;

namespace Aetherium.Server.Controllers
{
    /// <summary>
    /// REST API controller for managing training curricula.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CurriculumController : ControllerBase
    {
        /// <summary>
        /// Gets all curricula.
        /// </summary>
        [HttpGet]
        public ActionResult<List<CurriculumDefinition>> GetAllCurricula()
        {
            // TODO: Load from storage
            var curricula = new List<CurriculumDefinition>();
            return Ok(curricula);
        }

        /// <summary>
        /// Gets a curriculum by ID.
        /// </summary>
        [HttpGet("{curriculumId}")]
        public ActionResult<CurriculumDefinition> GetCurriculum(string curriculumId)
        {
            // TODO: Load from storage
            var curriculum = LoadCurriculumFromFile(curriculumId);
            
            if (curriculum == null)
                return NotFound($"Curriculum not found: {curriculumId}");

            return Ok(curriculum);
        }

        /// <summary>
        /// Creates or updates a curriculum.
        /// </summary>
        [HttpPost]
        public ActionResult<CurriculumDefinition> CreateCurriculum([FromBody] CurriculumDefinition curriculum)
        {
            var errors = curriculum.Validate();
            if (errors.Count > 0)
            {
                return BadRequest(new { errors });
            }

            // TODO: Save to storage
            SaveCurriculumToFile(curriculum);
            return CreatedAtAction(nameof(GetCurriculum), new { curriculumId = curriculum.CurriculumId }, curriculum);
        }

        private CurriculumDefinition? LoadCurriculumFromFile(string curriculumId)
        {
            var filePath = Path.Combine("Data", "Curricula", $"{curriculumId}.json");
            
            if (!System.IO.File.Exists(filePath))
                return null;

            try
            {
                var json = System.IO.File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<CurriculumDefinition>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        private void SaveCurriculumToFile(CurriculumDefinition curriculum)
        {
            var directory = Path.Combine("Data", "Curricula");
            Directory.CreateDirectory(directory);
            
            var filePath = Path.Combine(directory, $"{curriculum.CurriculumId}.json");
            var json = JsonSerializer.Serialize(curriculum, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            System.IO.File.WriteAllText(filePath, json);
        }
    }
}

