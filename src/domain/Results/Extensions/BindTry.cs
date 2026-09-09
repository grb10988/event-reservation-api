namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result, bind with exception safety
    // ============================================================

    public static Result BindTry(this Result result, Func<Result> func, Func<Exception, ResultError> exceptionHandler)
    {
        if (result.IsFailure)
            return result;

        try
        {
            return func();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure(exceptionHandler(ex));
        }
    }

    public static async Task<Result> BindTry(this Result result, Func<Task<Result>> func, Func<Exception, ResultError> exceptionHandler)
    {
        if (result.IsFailure)
            return result;

        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure(exceptionHandler(ex));
        }
    }

    public static async Task<Result> BindTry(this Task<Result> resultTask, Func<Result> func, Func<Exception, ResultError> exceptionHandler)
    {
        var result = await resultTask;
        return result.BindTry(func, exceptionHandler);
    }

    public static async Task<Result> BindTry(this Task<Result> resultTask, Func<Task<Result>> func, Func<Exception, ResultError> exceptionHandler)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return result;

        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure(exceptionHandler(ex));
        }
    }

    // ============================================================
    // Group 2: Result<TOld> -> Result<TNew>, bind with exception safety
    // ============================================================

    public static Result<TNew> BindTry<TOld, TNew>(this Result<TOld> result, Func<TOld, Result<TNew>> func, Func<Exception, ResultError> exceptionHandler)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        try
        {
            return func(result.Value);
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<TNew>(exceptionHandler(ex));
        }
    }

    public static async Task<Result<TNew>> BindTry<TOld, TNew>(this Result<TOld> result, Func<TOld, Task<Result<TNew>>> func, Func<Exception, ResultError> exceptionHandler)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        try
        {
            return await func(result.Value);
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<TNew>(exceptionHandler(ex));
        }
    }

    public static async Task<Result<TNew>> BindTry<TOld, TNew>(this Task<Result<TOld>> resultTask, Func<TOld, Result<TNew>> func, Func<Exception, ResultError> exceptionHandler)
    {
        var result = await resultTask;
        return result.BindTry(func, exceptionHandler);
    }

    public static async Task<Result<TNew>> BindTry<TOld, TNew>(this Task<Result<TOld>> resultTask, Func<TOld, Task<Result<TNew>>> func, Func<Exception, ResultError> exceptionHandler)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        try
        {
            return await func(result.Value);
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<TNew>(exceptionHandler(ex));
        }
    }
}