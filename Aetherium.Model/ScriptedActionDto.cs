using System.Collections.Generic;
using Orleans;

namespace Aetherium.Model
{
    /// <summary>
    /// A single scripted action: a tool id plus its arguments.
    /// Consumed by <c>IGameManagementGrain.ExecuteToolBatchAsync</c>.
    /// </summary>
    [GenerateSerializer]
    public class ScriptedActionDto
    {
        [Id(0)]
        public string Tool { get; set; } = string.Empty;
        [Id(1)]
        public Dictionary<string, object> Args { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Result of one step in a batch action sequence.
    /// </summary>
    [GenerateSerializer]
    public class BatchActionResultDto
    {
        [Id(0)]
        public int Index { get; set; }
        [Id(1)]
        public string Tool { get; set; } = string.Empty;
        [Id(2)]
        public bool Success { get; set; }
        [Id(3)]
        public string Message { get; set; } = string.Empty;
    }
}
