using System.Collections.Generic;
using System.Linq;
using Aetherium.Model.Training;

namespace Aetherium.Dashboard
{
    /// <summary>
    /// Exposes the built-in benchmark catalog (<see cref="BenchmarkLibrary"/>) to the dashboard.
    /// The library is an in-process static registry, so no Orleans client is needed. Backs the
    /// Benchmark Comparison page (P3-10).
    /// </summary>
    public class BenchmarkCatalogService
    {
        public virtual List<BenchmarkScenario> GetAllBenchmarks() => BenchmarkLibrary.GetAllBenchmarks();

        /// <summary>Distinct benchmark categories across the catalog, sorted for stable display.</summary>
        public virtual List<string> GetCategories() =>
            GetAllBenchmarks()
                .SelectMany(b => b.Categories)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

        /// <summary>Catalog filtered to a category, or the whole catalog when no category is given.</summary>
        public virtual List<BenchmarkScenario> GetByCategory(string? category) =>
            string.IsNullOrWhiteSpace(category)
                ? GetAllBenchmarks()
                : GetAllBenchmarks().Where(b => b.Categories.Contains(category)).ToList();
    }
}
