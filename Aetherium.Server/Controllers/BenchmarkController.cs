using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Aetherium.WorldGen.Training;
using Aetherium.Model.Training;
using System.Text.Json;

namespace Aetherium.Server.Controllers
{
    /// <summary>
    /// REST API controller for managing benchmark scenarios.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BenchmarkController : ControllerBase
    {
        /// <summary>
        /// Gets all benchmarks.
        /// </summary>
        [HttpGet]
        public ActionResult<List<BenchmarkScenario>> GetAllBenchmarks()
        {
            var benchmarks = BenchmarkLibrary.GetAllBenchmarks();
            return Ok(benchmarks);
        }

        /// <summary>
        /// Gets a benchmark by ID.
        /// </summary>
        [HttpGet("{benchmarkId}")]
        public ActionResult<BenchmarkScenario> GetBenchmark(string benchmarkId)
        {
            var benchmark = BenchmarkLibrary.GetBenchmark(benchmarkId);
            
            if (benchmark == null)
                return NotFound($"Benchmark not found: {benchmarkId}");

            return Ok(benchmark);
        }

        /// <summary>
        /// Gets benchmarks by category.
        /// </summary>
        [HttpGet("category/{category}")]
        public ActionResult<List<BenchmarkScenario>> GetBenchmarksByCategory(string category)
        {
            var benchmarks = BenchmarkLibrary.GetBenchmarksByCategory(category);
            return Ok(benchmarks);
        }

        /// <summary>
        /// Creates a new benchmark from a recipe.
        /// </summary>
        [HttpPost]
        public ActionResult<BenchmarkScenario> CreateBenchmark([FromBody] CreateBenchmarkRequest request)
        {
            var benchmark = BenchmarkGenerator.GenerateBenchmark(
                request.BenchmarkId,
                request.Name,
                request.Description,
                request.Recipe,
                request.SuccessCriteria,
                request.Difficulty);

            BenchmarkLibrary.RegisterBenchmark(benchmark);
            return CreatedAtAction(nameof(GetBenchmark), new { benchmarkId = benchmark.BenchmarkId }, benchmark);
        }

        /// <summary>
        /// Generates variations of a benchmark.
        /// </summary>
        [HttpPost("{benchmarkId}/variations")]
        public ActionResult<List<BenchmarkScenario>> GenerateVariations(
            string benchmarkId,
            [FromBody] GenerateVariationsRequest request)
        {
            var baseBenchmark = BenchmarkLibrary.GetBenchmark(benchmarkId);
            
            if (baseBenchmark == null)
                return NotFound($"Benchmark not found: {benchmarkId}");

            var variations = BenchmarkGenerator.GenerateVariations(
                baseBenchmark,
                request.VariationCount,
                request.SeedOffset);

            return Ok(variations);
        }

        public class CreateBenchmarkRequest
        {
            public string BenchmarkId { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public BenchmarkRecipe Recipe { get; set; } = new BenchmarkRecipe();
            public SuccessCriteria SuccessCriteria { get; set; } = new SuccessCriteria();
            public int Difficulty { get; set; } = 5;
        }

        public class GenerateVariationsRequest
        {
            public int VariationCount { get; set; } = 5;
            public int? SeedOffset { get; set; }
        }
    }
}

