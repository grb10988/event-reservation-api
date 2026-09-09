namespace EventReservation.Domain.Results;

public static partial class ResultFactory
{
    public static Result Try(Action action, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            action();
            return Success();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure(errorHandler(ex));
        }
    }

    public static Result<T> Try<T>(Func<T> func, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return Success(func());
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }

    public static Result Try(Func<Result> func, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure(errorHandler(ex));
        }
    }

    public static Result<T> Try<T>(Func<Result<T>> func, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }

    public static async Task<Result> Try(Func<Task> action, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            await action().ConfigureAwait(false);
            return Success();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure(errorHandler(ex));
        }
    }

    public static async Task<Result<T>> Try<T>(Func<Task<T>> func, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return Success(await func().ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }

    public static async Task<Result> Try(Func<Task<Result>> func, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return await func().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure(errorHandler(ex));
        }
    }

    public static async Task<Result<T>> Try<T>(Func<Task<Result<T>>> func, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return await func().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }
}