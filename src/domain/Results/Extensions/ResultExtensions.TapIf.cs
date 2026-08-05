namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result, conditional side effect on a plain bool
    // ============================================================

    public static Result TapIf(this Result result, bool condition, Action action)
    {
        if (result.IsSuccess && condition)
            action();

        return result;
    }

    public static async Task<Result> TapIf(this Result result, bool condition, Func<Task> action)
    {
        if (result.IsSuccess && condition)
            await action();

        return result;
    }

    public static async Task<Result> TapIf(this Task<Result> resultTask, bool condition, Action action)
    {
        var result = await resultTask;
        return result.TapIf(condition, action);
    }

    public static async Task<Result> TapIf(this Task<Result> resultTask, bool condition, Func<Task> action)
    {
        var result = await resultTask;

        if (result.IsSuccess && condition)
            await action();

        return result;
    }

    // ============================================================
    // Group 2: Result<T> -> Result<T>, conditional side effect on the value
    // ============================================================

    public static Result<T> TapIf<T>(this Result<T> result, Func<T, bool> predicate, Action<T> action)
    {
        if (result.IsSuccess && predicate(result.Value))
            action(result.Value);

        return result;
    }

    public static async Task<Result<T>> TapIf<T>(this Result<T> result, Func<T, bool> predicate, Func<T, Task> action)
    {
        if (result.IsSuccess && predicate(result.Value))
            await action(result.Value);

        return result;
    }

    public static async Task<Result<T>> TapIf<T>(this Task<Result<T>> resultTask, Func<T, bool> predicate, Action<T> action)
    {
        var result = await resultTask;
        return result.TapIf(predicate, action);
    }

    public static async Task<Result<T>> TapIf<T>(this Task<Result<T>> resultTask, Func<T, bool> predicate, Func<T, Task> action)
    {
        var result = await resultTask;

        if (result.IsSuccess && predicate(result.Value))
            await action(result.Value);

        return result;
    }
}