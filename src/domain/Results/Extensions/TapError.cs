namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result, side effect on failure (errors ignored)
    // ============================================================

    public static Result TapError(this Result result, Action action)
    {
        if (result.IsFailure)
            action();

        return result;
    }

    // ============================================================
    // Group 2: Result -> Result, side effect on failure (errors consumed)
    // ============================================================

    public static Result TapError(this Result result, Action<IReadOnlyCollection<ResultError>> action)
    {
        if (result.IsFailure)
            action(result.Errors);

        return result;
    }

    // ============================================================
    // Group 3: Result<T> -> Result<T>, side effect on failure (errors ignored)
    // ============================================================

    public static Result<T> TapError<T>(this Result<T> result, Action action)
    {
        if (result.IsFailure)
            action();

        return result;
    }

    // ============================================================
    // Group 4: Result<T> -> Result<T>, side effect on failure (errors consumed)
    // ============================================================

    public static Result<T> TapError<T>(this Result<T> result, Action<IReadOnlyCollection<ResultError>> action)
    {
        if (result.IsFailure)
            action(result.Errors);

        return result;
    }
}