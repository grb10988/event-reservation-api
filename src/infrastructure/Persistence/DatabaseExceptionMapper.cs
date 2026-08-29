using Npgsql;

namespace EventReservation.Infrastructure.Persistence;

internal static class DatabaseExceptionMapper
{
    public static ResultError Map(Exception ex)
    {
        if (ex is PostgresException pgEx)
        {
            return pgEx.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation => Errors.DuplicateRecord,
                PostgresErrorCodes.ForeignKeyViolation => Errors.ReferencedRecordMissing,
                PostgresErrorCodes.CheckViolation => Errors.GenericDatabaseError,
                PostgresErrorCodes.NotNullViolation => Errors.GenericDatabaseError,
                PostgresErrorCodes.DeadlockDetected => Errors.ConcurrencyConflict,
                PostgresErrorCodes.SerializationFailure => Errors.ConcurrencyConflict,
                PostgresErrorCodes.LockNotAvailable => Errors.ConcurrencyConflict,
                PostgresErrorCodes.QueryCanceled => Errors.GenericDatabaseError,
                PostgresErrorCodes.TooManyConnections => Errors.DatabaseUnavailable,
                _ => Errors.GenericDatabaseError
            };
        }

        if (ex is NpgsqlException)
            return Errors.DatabaseUnavailable;

        return Errors.GenericDatabaseError;
    }

    public static class Errors
    {
        private const string Context = "DATABASE";
        public static ResultError GenericDatabaseError => new(Context, "A database error occurred. Please try again.");
        public static ResultError DuplicateRecord => new(Context, "A record with the same unique value(s) already exists.");
        public static ResultError ReferencedRecordMissing => new(Context, "A referenced record does not exist.");
        public static ResultError ConcurrencyConflict => new(Context, "The record was modified by another process. Please try again.");
        public static ResultError DatabaseUnavailable => new(Context, "The database is currently unavailable. Please try again shortly.");
    }
}