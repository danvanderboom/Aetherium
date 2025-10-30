namespace ConsoleGameServer.Management
{
    /// <summary>
    /// Represents the result of a grain operation.
    /// </summary>
    public class OperationResult
    {
        public bool Success { get; set; }
        public string? Reason { get; set; }

        public static OperationResult Ok() => new OperationResult { Success = true };
        
        public static OperationResult Error(string reason) => new OperationResult 
        { 
            Success = false, 
            Reason = reason 
        };
    }
}

