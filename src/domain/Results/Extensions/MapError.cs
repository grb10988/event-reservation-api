namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result, transform error content
    // ============================================================

    public static Result MapError(this Result result, Func<IReadOnlyCollection<ResultError>, IReadOnlyCollection<ResultError>> func)
    {
        if (result.IsSuccess)
            return result;

        return Failure(func(result.Errors));
    }

    // ============================================================
    // Group 2: Result<T> -> Result<T>, transform error content
    // ============================================================

    public static Result<T> MapError<T>(this Result<T> result, Func<IReadOnlyCollection<ResultError>, IReadOnlyCollection<ResultError>> func)
    {
        if (result.IsSuccess)
            return result;

        return Failure<T>(func(result.Errors));
    }
}