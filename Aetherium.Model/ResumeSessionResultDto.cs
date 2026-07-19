namespace Aetherium.Model
{
    /// <summary>
    /// Result of GameHub.ResumeSession. On success, <see cref="Perception"/> carries the
    /// resumed session's current frame (with its original MoveSequence continuity) so the
    /// client can pick up rendering without waiting for the next push — the fresh-session
    /// frames sent during the reconnect handshake are discarded by the client.
    /// </summary>
    public class ResumeSessionResultDto
    {
        public bool Success { get; set; }
        public string? Reason { get; set; }
        public PerceptionDto? Perception { get; set; }
    }
}
