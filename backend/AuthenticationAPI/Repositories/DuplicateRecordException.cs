using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AuthenticationAPI.Repositories
{
    /// <summary>
    /// Thrown by a repository when a write violates a unique index.
    /// The service layer catches this instead of depending on EF Core / Npgsql exception types.
    /// </summary>
    public class DuplicateRecordException : Exception
    {
        public DuplicateRecordException(string message, Exception? inner = null) : base(message, inner) { }
    }

    internal static class DbErrors
    {
        public static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
