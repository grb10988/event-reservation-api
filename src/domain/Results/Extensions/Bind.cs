namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result (no value, continuation returns Result)
    // ============================================================

    public static Result Bind(this Result result, Func<Result> func)
    {
        if (result.IsFailure)
            return result;

        return func();
    }

    public static Task<Result> Bind(this Result result, Func<Task<Result>> func)
    {
        if (result.IsFailure)
            return Task.FromResult(result);

        return func();
    }

    public static async Task<Result> Bind(this Task<Result> resultTask, Func<Result> func)
    {
        var result = await resultTask;
        return result.Bind(func);
    }

    public static async Task<Result> Bind(this Task<Result> resultTask, Func<Task<Result>> func)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return result;

        return await func();
    }

    // ============================================================
    // Group 2: Result<T> -> Result (drop the value, continuation returns Result)
    // ============================================================

    public static Result Bind<T>(this Result<T> result, Func<Result> func)
    {
        if (result.IsFailure)
            return Failure(result.Errors);

        return func();
    }

    public static Task<Result> Bind<T>(this Result<T> result, Func<Task<Result>> func)
    {
        if (result.IsFailure)
            return Task.FromResult(Failure(result.Errors));

        return func();
    }

    public static async Task<Result> Bind<T>(this Task<Result<T>> resultTask, Func<Result> func)
    {
        var result = await resultTask;
        return result.Bind(func);
    }

    public static async Task<Result> Bind<T>(this Task<Result<T>> resultTask, Func<Task<Result>> func)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return Failure(result.Errors);

        return await func();
    }

    // ============================================================
    // Group 3: Result -> Result<T> (produce a value from a valueless result)
    // ============================================================

    public static Result<T> Bind<T>(this Result result, Func<Result<T>> func)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        return func();
    }

    public static Task<Result<T>> Bind<T>(this Result result, Func<Task<Result<T>>> func)
    {
        if (result.IsFailure)
            return Task.FromResult(Failure<T>(result.Errors));

        return func();
    }

    public static async Task<Result<T>> Bind<T>(this Task<Result> resultTask, Func<Result<T>> func)
    {
        var result = await resultTask;
        return result.Bind(func);
    }

    public static async Task<Result<T>> Bind<T>(this Task<Result> resultTask, Func<Task<Result<T>>> func)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return Failure<T>(result.Errors);

        return await func();
    }

    // ============================================================
    // Group 4: Result<TOld> -> Result<TNew> (continuation consumes the value)
    // ============================================================

    public static Result<TNew> Bind<TOld, TNew>(this Result<TOld> result, Func<TOld, Result<TNew>> func)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        return func(result.Value);
    }

    public static Task<Result<TNew>> Bind<TOld, TNew>(this Result<TOld> result, Func<TOld, Task<Result<TNew>>> func)
    {
        if (result.IsFailure)
            return Task.FromResult(Failure<TNew>(result.Errors));

        return func(result.Value);
    }

    public static async Task<Result<TNew>> Bind<TOld, TNew>(this Task<Result<TOld>> resultTask, Func<TOld, Result<TNew>> func)
    {
        var result = await resultTask;
        return result.Bind(func);
    }

    public static async Task<Result<TNew>> Bind<TOld, TNew>(this Task<Result<TOld>> resultTask, Func<TOld, Task<Result<TNew>>> func)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        return await func(result.Value);
    }

    // ============================================================
    // Group 5: Result<TOld> -> Result<TNew> (continuation ignores the value)
    // ============================================================

    public static Result<TNew> Bind<TOld, TNew>(this Result<TOld> result, Func<Result<TNew>> func)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        return func();
    }

    public static Task<Result<TNew>> Bind<TOld, TNew>(this Result<TOld> result, Func<Task<Result<TNew>>> func)
    {
        if (result.IsFailure)
            return Task.FromResult(Failure<TNew>(result.Errors));

        return func();
    }

    public static async Task<Result<TNew>> Bind<TOld, TNew>(this Task<Result<TOld>> resultTask, Func<Result<TNew>> func)
    {
        var result = await resultTask;
        return result.Bind(func);
    }

    public static async Task<Result<TNew>> Bind<TOld, TNew>(this Task<Result<TOld>> resultTask, Func<Task<Result<TNew>>> func)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        return await func();
    }
}