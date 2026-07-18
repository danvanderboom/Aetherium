using System;

namespace Aetherium.Server.Management
{
    /// <summary>
    /// Gate for operator/"god-view" management operations (headless session creation,
    /// absolute-coordinate perception, world snapshots).
    ///
    /// These run over the trusted management path (localhost Orleans / dev tooling) and are
    /// enabled by default. Set <c>AETHERIUM_OPERATOR_DISABLED=1</c> to lock them down (e.g. in a
    /// hardened deployment). They are never exposed as player/agent tools, so ordinary player
    /// profiles cannot reach them regardless of this flag.
    /// </summary>
    public static class OperatorAccess
    {
        public const string DisableEnvVar = "AETHERIUM_OPERATOR_DISABLED";

        public static bool IsEnabled() =>
            Environment.GetEnvironmentVariable(DisableEnvVar) != "1";
    }
}
