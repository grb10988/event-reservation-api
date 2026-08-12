namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result, check func returns Result
    // ============================================================

    public static Result Ensure(this Result result, Func<Result> func)
    {
        if (result.IsFailure)
            return result;

        var checkResult = func();

        return checkResult.IsSuccess
            ? result
            : Failure(checkResult.Errors);
    }

    public static async Task<Result> Ensure(this Result result, Func<Task<Result>> func)
    {
        if (result.IsFailure)
            return result;

        var checkResult = await func();

        return checkResult.IsSuccess
            ? result
            : Failure(checkResult.Errors);
    }

    public static async Task<Result> Ensure(this Task<Result> resultTask, Func<Result> func)
    {
        var result = await resultTask;
        return result.Ensure(func);
    }

    public static async Task<Result> Ensure(this Task<Result> resultTask, Func<Task<Result>> func)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return result;

        var checkResult = await func();

        return checkResult.IsSuccess
            ? result
            : Failure(checkResult.Errors);
    }

    // ============================================================
    // Group 2: Result<T> -> Result<T>, check func returns Result
    // ============================================================

    public static Result<T> Ensure<T>(this Result<T> result, Func<T, Result> func)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        var checkResult = func(result.Value);

        return checkResult.IsSuccess
            ? result
            : Failure<T>(checkResult.Errors);
    }

    public static async Task<Result<T>> Ensure<T>(this Result<T> result, Func<T, Task<Result>> func)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        var checkResult = await func(result.Value);

        return checkResult.IsSuccess
            ? result
            : Failure<T>(checkResult.Errors);
    }

    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> resultTask, Func<T, Result> func)
    {
        var result = await resultTask;
        return result.Ensure(func);
    }

    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> resultTask, Func<T, Task<Result>> func)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return Failure<T>(result.Errors);

        var checkResult = await func(result.Value);

        return checkResult.IsSuccess
            ? result
            : Failure<T>(checkResult.Errors);
    }
}