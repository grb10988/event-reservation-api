namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result, check func returns Result<TOther> (discarded)
    // ============================================================

    public static Result Check<TOther>(this Result result, Func<Result<TOther>> func)
    {
        if (result.IsFailure)
            return result;

        var checkResult = func();

        return checkResult.IsSuccess
            ? result
            : Failure(checkResult.Errors);
    }

    public static async Task<Result> Check<TOther>(this Result result, Func<Task<Result<TOther>>> func)
    {
        if (result.IsFailure)
            return result;

        var checkResult = await func();

        return checkResult.IsSuccess
            ? result
            : Failure(checkResult.Errors);
    }

    public static async Task<Result> Check<TOther>(this Task<Result> resultTask, Func<Result<TOther>> func)
    {
        var result = await resultTask;
        return result.Check(func);
    }

    public static async Task<Result> Check<TOther>(this Task<Result> resultTask, Func<Task<Result<TOther>>> func)
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
    // Group 2: Result<T> -> Result<T>, check func returns Result<TOther> (discarded)
    // ============================================================

    public static Result<T> Check<T, TOther>(this Result<T> result, Func<T, Result<TOther>> func)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        var checkResult = func(result.Value);

        return checkResult.IsSuccess
            ? result
            : Failure<T>(checkResult.Errors);
    }

    public static async Task<Result<T>> Check<T, TOther>(this Result<T> result, Func<T, Task<Result<TOther>>> func)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        var checkResult = await func(result.Value);

        return checkResult.IsSuccess
            ? result
            : Failure<T>(checkResult.Errors);
    }

    public static async Task<Result<T>> Check<T, TOther>(this Task<Result<T>> resultTask, Func<T, Result<TOther>> func)
    {
        var result = await resultTask;
        return result.Check(func);
    }

    public static async Task<Result<T>> Check<T, TOther>(this Task<Result<T>> resultTask, Func<T, Task<Result<TOther>>> func)
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