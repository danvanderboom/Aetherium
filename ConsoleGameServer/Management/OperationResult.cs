namespace ConsoleGameServer.Management
{
    /// <summary>
    /// Represents the result of an operation with success/failure status and an optional message.
    /// </summary>
    public struct OperationResult
    {
        public bool Success { get; init; }
        public string Message { get; init; }

        private OperationResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static OperationResult Ok(string message = "") => new OperationResult(true, message);
        public static OperationResult Error(string message) => new OperationResult(false, message);
    }
}
