using System;

namespace Aetherium.Server.Persistence
{
    /// <summary>
    /// Thrown when the persistence layer detects a stored snapshot or delta whose
    /// schema version exceeds what the running binary supports. Indicates either a
    /// downgrade attempt or a corrupted store; the caller should NOT silently skip
    /// or partially apply the affected row.
    /// </summary>
    public sealed class PersistenceVersionMismatchException : Exception
    {
        public PersistenceVersionMismatchException(string message) : base(message) { }
        public PersistenceVersionMismatchException(string message, Exception innerException) : base(message, innerException) { }
    }
}
