namespace EventReservation.Domain.Results;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result, check via predicate + error
    // ============================================================

    public static Result Ensure(this Result result, Func<bool> predicate, ResultError error)
    {
        if (result.IsFailure)
            return result;

        return predicate()
            ? result
            : Failure(error);
    }

    public static async Task<Result> Ensure(this Result result, Func<Task<bool>> predicate, ResultError error)
    {
        if (result.IsFailure)
            return result;

        return await predicate()
            ? result
            : Failure(error);
    }

    public static async Task<Result> Ensure(this Task<Result> resultTask, Func<bool> predicate, ResultError error)
    {
        var result = await resultTask;
        return result.Ensure(predicate, error);
    }

    public static async Task<Result> Ensure(this Task<Result> resultTask, Func<Task<bool>> predicate, ResultError error)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return result;

        return await predicate()
            ? result
            : Failure(error);
    }

    // ============================================================
    // Group 2: Result<T> -> Result<T>, check via predicate + error
    // ============================================================

    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, ResultError error)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        return predicate(result.Value)
            ? result
            : Failure<T>(error);
    }

    public static async Task<Result<T>> Ensure<T>(this Result<T> result, Func<T, Task<bool>> predicate, ResultError error)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        return await predicate(result.Value)
            ? result
            : Failure<T>(error);
    }

    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> resultTask, Func<T, bool> predicate, ResultError error)
    {
        var result = await resultTask;
        return result.Ensure(predicate, error);
    }

    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> resultTask, Func<T, Task<bool>> predicate, ResultError error)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return Failure<T>(result.Errors);

        return await predicate(result.Value)
            ? result
            : Failure<T>(error);
    }
}