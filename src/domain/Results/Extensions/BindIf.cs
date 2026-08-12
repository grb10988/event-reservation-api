namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result, conditional on a plain bool
    // ============================================================

    public static Result BindIf(this Result result, bool condition, Func<Result> func)
    {
        if (result.IsFailure)
            return result;

        return condition
            ? func()
            : result;
    }

    public static async Task<Result> BindIf(this Result result, bool condition, Func<Task<Result>> func)
    {
        if (result.IsFailure)
            return result;

        return condition
            ? await func()
            : result;
    }

    public static async Task<Result> BindIf(this Task<Result> resultTask, bool condition, Func<Result> func)
    {
        var result = await resultTask;
        return result.BindIf(condition, func);
    }

    public static async Task<Result> BindIf(this Task<Result> resultTask, bool condition, Func<Task<Result>> func)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return result;

        return condition
            ? await func()
            : result;
    }

    // ============================================================
    // Group 2: Result<T> -> Result<T>, conditional on the value
    // ============================================================

    public static Result<T> BindIf<T>(this Result<T> result, Func<T, bool> predicate, Func<T, Result<T>> func)
    {
        if (result.IsFailure)
            return result;

        return predicate(result.Value)
            ? func(result.Value)
            : result;
    }

    public static async Task<Result<T>> BindIf<T>(this Result<T> result, Func<T, bool> predicate, Func<T, Task<Result<T>>> func)
    {
        if (result.IsFailure)
            return result;

        return predicate(result.Value)
            ? await func(result.Value)
            : result;
    }

    public static async Task<Result<T>> BindIf<T>(this Task<Result<T>> resultTask, Func<T, bool> predicate, Func<T, Result<T>> func)
    {
        var result = await resultTask;
        return result.BindIf(predicate, func);
    }

    public static async Task<Result<T>> BindIf<T>(this Task<Result<T>> resultTask, Func<T, bool> predicate, Func<T, Task<Result<T>>> func)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return result;

        return predicate(result.Value)
            ? await func(result.Value)
            : result;
    }
}