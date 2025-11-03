using Aetherium.Unity.Model;

namespace Aetherium.Unity.Networking
{
    /// <summary>
    /// Interface for providing perception updates to the game client.
    /// </summary>
    public interface IPerceptionProvider
    {
        /// <summary>
        /// Gets the current perception state.
        /// </summary>
        PerceptionLite? GetCurrent();

        /// <summary>
        /// Event fired when perception is updated.
        /// </summary>
        event System.Action<PerceptionLite>? PerceptionUpdated;
    }
}

