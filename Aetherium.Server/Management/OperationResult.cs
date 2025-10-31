using Orleans;

namespace Aetherium.Server.Management
{
    /// <summary>
    /// Represents the result of an operation with success/failure status and an optional message.
    /// </summary>
    [GenerateSerializer]
    public struct OperationResult
    {
        [Id(0)]
        public bool Success { get; init; }
        
        [Id(1)]
        public string Message { get; init; } = string.Empty;

        private OperationResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static OperationResult Ok(string message = "") => new OperationResult(true, message);
        public static OperationResult Error(string message) => new OperationResult(false, message);
    }
}

