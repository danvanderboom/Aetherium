using Orleans;

namespace Aetherium.Server.Management
{
    /// <summary>
    /// Result of provisioning a headless (client-less) game session.
    /// </summary>
    [GenerateSerializer]
    public class HeadlessSessionResult
    {
        [Id(0)] public bool Success { get; set; }
        [Id(1)] public string Message { get; set; } = string.Empty;
        [Id(2)] public string? SessionId { get; set; }
        [Id(3)] public string? WorldId { get; set; }
        [Id(4)] public string? ConnectionId { get; set; }

        public static HeadlessSessionResult Ok(string sessionId, string worldId, string connectionId) =>
            new HeadlessSessionResult { Success = true, SessionId = sessionId, WorldId = worldId, ConnectionId = connectionId };

        public static HeadlessSessionResult Error(string message) =>
            new HeadlessSessionResult { Success = false, Message = message };
    }
}
