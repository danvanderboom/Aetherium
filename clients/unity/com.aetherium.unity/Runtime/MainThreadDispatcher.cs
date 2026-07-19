using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Aetherium.Unity
{
    /// <summary>
    /// Marshals Aetherium.Client callbacks (which fire on SignalR worker threads — the core
    /// is deliberately sync-context-free) onto Unity's main thread. Producers enqueue from
    /// any thread; the queue drains once per frame in Update. Owned and driven by
    /// <see cref="AetheriumClientBehaviour"/> — games never touch this directly.
    /// </summary>
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        /// <summary>Thread-safe: queues <paramref name="action"/> for the next Update.</summary>
        public void Enqueue(Action action)
        {
            if (action != null)
                _queue.Enqueue(action);
        }

        private void Update()
        {
            // Drain everything queued so far; anything enqueued during a drain waits a frame,
            // which keeps a chatty burst of frames from starving rendering.
            int budget = _queue.Count;
            while (budget-- > 0 && _queue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
