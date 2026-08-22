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

    public static async Task<Result> TapError(this Result result, Func<Task> action)
    {
        if (result.IsFailure)
            await action();

        return result;
    }

    public static async Task<Result> TapError(this Task<Result> resultTask, Action action)
    {
        var result = await resultTask;
        return result.TapError(action);
    }

    public static async Task<Result> TapError(this Task<Result> resultTask, Func<Task> action)
    {
        var result = await resultTask;

        if (result.IsFailure)
            await action();

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

    public static async Task<Result> TapError(this Result result, Func<IReadOnlyCollection<ResultError>, Task> action)
    {
        if (result.IsFailure)
            await action(result.Errors);

        return result;
    }

    public static async Task<Result> TapError(this Task<Result> resultTask, Action<IReadOnlyCollection<ResultError>> action)
    {
        var result = await resultTask;
        return result.TapError(action);
    }

    public static async Task<Result> TapError(this Task<Result> resultTask, Func<IReadOnlyCollection<ResultError>, Task> action)
    {
        var result = await resultTask;

        if (result.IsFailure)
            await action(result.Errors);

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

    public static async Task<Result<T>> TapError<T>(this Result<T> result, Func<Task> action)
    {
        if (result.IsFailure)
            await action();

        return result;
    }

    public static async Task<Result<T>> TapError<T>(this Task<Result<T>> resultTask, Action action)
    {
        var result = await resultTask;
        return result.TapError(action);
    }

    public static async Task<Result<T>> TapError<T>(this Task<Result<T>> resultTask, Func<Task> action)
    {
        var result = await resultTask;

        if (result.IsFailure)
            await action();

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

    public static async Task<Result<T>> TapError<T>(this Result<T> result, Func<IReadOnlyCollection<ResultError>, Task> action)
    {
        if (result.IsFailure)
            await action(result.Errors);

        return result;
    }

    public static async Task<Result<T>> TapError<T>(this Task<Result<T>> resultTask, Action<IReadOnlyCollection<ResultError>> action)
    {
        var result = await resultTask;
        return result.TapError(action);
    }

    public static async Task<Result<T>> TapError<T>(this Task<Result<T>> resultTask, Func<IReadOnlyCollection<ResultError>, Task> action)
    {
        var result = await resultTask;

        if (result.IsFailure)
            await action(result.Errors);

        return result;
    }
}