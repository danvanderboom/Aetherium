using System.Collections.Generic;

namespace Aetherium.WorldGen
{
    public sealed class GenerationValidationResult
    {
        public bool Success => Errors.Count == 0;
        public List<string> Errors { get; } = new();
        public Dictionary<string, object> ProofArtifacts { get; } = new();

        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Errors.Add(error);
            }
        }
    }
}



