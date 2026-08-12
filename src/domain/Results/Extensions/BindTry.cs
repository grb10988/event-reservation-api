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
    // Group 2: Result<T> -> Result<T>, bind with exception safety
    // ============================================================

    public static Result<T> BindTry<T>(this Result<T> result, Func<T, Result<T>> func, Func<Exception, ResultError> exceptionHandler)
    {
        if (result.IsFailure)
            return result;

        try
        {
            return func(result.Value);
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(exceptionHandler(ex));
        }
    }

    public static async Task<Result<T>> BindTry<T>(this Result<T> result, Func<T, Task<Result<T>>> func, Func<Exception, ResultError> exceptionHandler)
    {
        if (result.IsFailure)
            return result;

        try
        {
            return await func(result.Value);
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(exceptionHandler(ex));
        }
    }

    public static async Task<Result<T>> BindTry<T>(this Task<Result<T>> resultTask, Func<T, Result<T>> func, Func<Exception, ResultError> exceptionHandler)
    {
        var result = await resultTask;
        return result.BindTry(func, exceptionHandler);
    }

    public static async Task<Result<T>> BindTry<T>(this Task<Result<T>> resultTask, Func<T, Task<Result<T>>> func, Func<Exception, ResultError> exceptionHandler)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return result;

        try
        {
            return await func(result.Value);
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(exceptionHandler(ex));
        }
    }
}