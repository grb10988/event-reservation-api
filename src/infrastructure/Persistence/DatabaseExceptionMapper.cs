using Microsoft.Extensions.Logging;
using Npgsql;

namespace EventReservation.Infrastructure.Persistence;

internal static class DatabaseExceptionMapper
{
    public static ResultError Map(Exception ex, ILogger logger)
    {
        var error = MapCore(ex);

        if (error.Category is ErrorCategory.Unexpected or ErrorCategory.Unavailable)
            logger.LogError(
                ex,
                "Database operation failed and was mapped to {Category}: {Message}",
                error.Category,
                error.Message);

        return error;
    }

    private static ResultError MapCore(Exception ex)
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

        public static ResultError GenericDatabaseError =>
            new(Context, "A database error occurred. Please try again.", ErrorCategory.Unexpected);

        public static ResultError DuplicateRecord =>
            new(Context, "A record with the same unique value(s) already exists.", ErrorCategory.Conflict);

        public static ResultError ReferencedRecordMissing =>
            new(Context, "A referenced record does not exist.", ErrorCategory.Unprocessable);

        public static ResultError ConcurrencyConflict =>
            new(Context, "The record was modified by another process. Please try again.", ErrorCategory.Conflict);

        public static ResultError DatabaseUnavailable =>
            new(Context, "The database is currently unavailable. Please try again shortly.", ErrorCategory.Unavailable);
    }
}