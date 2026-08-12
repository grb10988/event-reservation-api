namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result<TNew>, map with exception safety
    // ============================================================

    public static Result<TNew> MapTry<TNew>(this Result result, Func<TNew> func, Func<Exception, ResultError> exceptionHandler)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        try
        {
            return Success(func());
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<TNew>(exceptionHandler(ex));
        }
    }

    public static async Task<Result<TNew>> MapTry<TNew>(this Result result, Func<Task<TNew>> func, Func<Exception, ResultError> exceptionHandler)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        try
        {
            return Success(await func());
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<TNew>(exceptionHandler(ex));
        }
    }

    public static async Task<Result<TNew>> MapTry<TNew>(this Task<Result> resultTask, Func<TNew> func, Func<Exception, ResultError> exceptionHandler)
    {
        var result = await resultTask;
        return result.MapTry(func, exceptionHandler);
    }

    public static async Task<Result<TNew>> MapTry<TNew>(this Task<Result> resultTask, Func<Task<TNew>> func, Func<Exception, ResultError> exceptionHandler)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        try
        {
            return Success(await func());
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<TNew>(exceptionHandler(ex));
        }
    }

    // ============================================================
    // Group 2: Result<T> -> Result<TNew>, map with exception safety
    // ============================================================

    public static Result<TNew> MapTry<T, TNew>(this Result<T> result, Func<T, TNew> func, Func<Exception, ResultError> exceptionHandler)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        try
        {
            return Success(func(result.Value));
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<TNew>(exceptionHandler(ex));
        }
    }

    public static async Task<Result<TNew>> MapTry<T, TNew>(this Result<T> result, Func<T, Task<TNew>> func, Func<Exception, ResultError> exceptionHandler)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        try
        {
            return Success(await func(result.Value));
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<TNew>(exceptionHandler(ex));
        }
    }

    public static async Task<Result<TNew>> MapTry<T, TNew>(this Task<Result<T>> resultTask, Func<T, TNew> func, Func<Exception, ResultError> exceptionHandler)
    {
        var result = await resultTask;
        return result.MapTry(func, exceptionHandler);
    }

    public static async Task<Result<TNew>> MapTry<T, TNew>(this Task<Result<T>> resultTask, Func<T, Task<TNew>> func, Func<Exception, ResultError> exceptionHandler)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        try
        {
            return Success(await func(result.Value));
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<TNew>(exceptionHandler(ex));
        }
    }
}