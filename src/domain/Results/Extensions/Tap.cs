namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result (side effect, no value)
    // ============================================================

    public static Result Tap(this Result result, Action action)
    {
        if (result.IsSuccess)
            action();

        return result;
    }

    public static async Task<Result> Tap(this Result result, Func<Task> action)
    {
        if (result.IsSuccess)
            await action();

        return result;
    }

    public static async Task<Result> Tap(this Task<Result> resultTask, Action action)
    {
        var result = await resultTask;
        return result.Tap(action);
    }

    public static async Task<Result> Tap(this Task<Result> resultTask, Func<Task> action)
    {
        var result = await resultTask;

        if (result.IsSuccess)
            await action();

        return result;
    }

    // ============================================================
    // Group 2: Result<T> -> Result<T> (side effect consumes the value)
    // ============================================================

    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
    {
        if (result.IsSuccess)
            action(result.Value);

        return result;
    }

    public static async Task<Result<T>> Tap<T>(this Result<T> result, Func<T, Task> action)
    {
        if (result.IsSuccess)
            await action(result.Value);

        return result;
    }

    public static async Task<Result<T>> Tap<T>(this Task<Result<T>> resultTask, Action<T> action)
    {
        var result = await resultTask;
        return result.Tap(action);
    }

    public static async Task<Result<T>> Tap<T>(this Task<Result<T>> resultTask, Func<T, Task> action)
    {
        var result = await resultTask;

        if (result.IsSuccess)
            await action(result.Value);

        return result;
    }

    // ============================================================
    // Group 3: Result<T> -> Result<T> (side effect ignores the value)
    // ============================================================

    public static Result<T> Tap<T>(this Result<T> result, Action action)
    {
        if (result.IsSuccess)
            action();

        return result;
    }

    public static async Task<Result<T>> Tap<T>(this Result<T> result, Func<Task> action)
    {
        if (result.IsSuccess)
            await action();

        return result;
    }

    public static async Task<Result<T>> Tap<T>(this Task<Result<T>> resultTask, Action action)
    {
        var result = await resultTask;
        return result.Tap(action);
    }

    public static async Task<Result<T>> Tap<T>(this Task<Result<T>> resultTask, Func<Task> action)
    {
        var result = await resultTask;

        if (result.IsSuccess)
            await action();

        return result;
    }
}