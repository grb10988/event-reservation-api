namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result<T> -> Result<T>, conditional same-type transform
    // ============================================================

    public static Result<T> MapIf<T>(this Result<T> result, Func<T, bool> predicate, Func<T, T> func)
    {
        if (result.IsFailure)
            return result;

        return predicate(result.Value)
            ? Success(func(result.Value))
            : result;
    }

    public static async Task<Result<T>> MapIf<T>(this Result<T> result, Func<T, bool> predicate, Func<T, Task<T>> func)
    {
        if (result.IsFailure)
            return result;

        return predicate(result.Value)
            ? Success(await func(result.Value))
            : result;
    }

    public static async Task<Result<T>> MapIf<T>(this Task<Result<T>> resultTask, Func<T, bool> predicate, Func<T, T> func)
    {
        var result = await resultTask;
        return result.MapIf(predicate, func);
    }

    public static async Task<Result<T>> MapIf<T>(this Task<Result<T>> resultTask, Func<T, bool> predicate, Func<T, Task<T>> func)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return result;

        return predicate(result.Value)
            ? Success(await func(result.Value))
            : result;
    }
}