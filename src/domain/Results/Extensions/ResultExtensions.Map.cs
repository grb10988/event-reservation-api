namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result<TOld> -> Result<TNew> (transform the value, can't fail)
    // ============================================================

    public static Result<TNew> Map<TOld, TNew>(this Result<TOld> result, Func<TOld, TNew> func)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        return Success(func(result.Value));
    }

    public static async Task<Result<TNew>> Map<TOld, TNew>(this Result<TOld> result, Func<TOld, Task<TNew>> func)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        return Success(await func(result.Value));
    }

    public static async Task<Result<TNew>> Map<TOld, TNew>(this Task<Result<TOld>> resultTask, Func<TOld, TNew> func)
    {
        var result = await resultTask;
        return result.Map(func);
    }

    public static async Task<Result<TNew>> Map<TOld, TNew>(this Task<Result<TOld>> resultTask, Func<TOld, Task<TNew>> func)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        return Success(await func(result.Value));
    }

    // ============================================================
    // Group 2: Result -> Result<TNew> (produce a value from a valueless result, can't fail)
    // ============================================================

    public static Result<TNew> Map<TNew>(this Result result, Func<TNew> func)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        return Success(func());
    }

    public static async Task<Result<TNew>> Map<TNew>(this Result result, Func<Task<TNew>> func)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        return Success(await func());
    }

    public static async Task<Result<TNew>> Map<TNew>(this Task<Result> resultTask, Func<TNew> func)
    {
        var result = await resultTask;
        return result.Map(func);
    }

    public static async Task<Result<TNew>> Map<TNew>(this Task<Result> resultTask, Func<Task<TNew>> func)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        return Success(await func());
    }
}