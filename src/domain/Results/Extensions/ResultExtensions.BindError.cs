namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result, recover from failure
    // ============================================================

    public static Result BindError(this Result result, Func<IReadOnlyCollection<ResultError>, Result> func)
    {
        if (result.IsSuccess)
            return result;

        return func(result.Errors);
    }

    // ============================================================
    // Group 2: Result<T> -> Result<T>, recover from failure
    // ============================================================

    public static Result<T> BindError<T>(this Result<T> result, Func<IReadOnlyCollection<ResultError>, Result<T>> func)
    {
        if (result.IsSuccess)
            return result;

        return func(result.Errors);
    }
}