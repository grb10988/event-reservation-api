namespace EventReservation.Domain.Results;

public static partial class ResultFactory
{
    // ============================================================
    // Group 1: Asynchronous, raw value
    // ============================================================

    public static async Task<Result> TryCancelable(Func<Task> func, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            await func();
            return Success();
        }
        catch (OperationCanceledException)
        {
            return Success();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure(errorHandler(ex));
        }
    }

    public static async Task<Result> TryCancelable(Func<CancellationToken, Task> func, CancellationToken cancellationToken, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            await func(cancellationToken);
            return Success();
        }
        catch (OperationCanceledException)
        {
            return Success();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure(errorHandler(ex));
        }
    }

    public static async Task<Result<T>> TryCancelable<T>(Func<Task<T>> func, Func<T> cancellationValueFactory, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return Success(await func());
        }
        catch (OperationCanceledException)
        {
            return Success(cancellationValueFactory());
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }

    public static async Task<Result<T>> TryCancelable<T>(Func<Task<T>> func, T cancellationDefaultValue, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return Success(await func());
        }
        catch (OperationCanceledException)
        {
            return Success(cancellationDefaultValue);
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }

    public static async Task<Result<T>> TryCancelable<T>(Func<CancellationToken, Task<T>> func, CancellationToken cancellationToken, Func<T> cancellationTokenValueFactory, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return Success(await func(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return Success(cancellationTokenValueFactory());
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }

    // ============================================================
    // Group 2: Asynchronous, Result-producing
    // ============================================================

    public static async Task<Result> TryCancelable(Func<Task<Result>> func, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return await func();
        }
        catch (OperationCanceledException)
        {
            return Success();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure(errorHandler(ex));
        }
    }

    public static async Task<Result<T>> TryCancelable<T>(Func<Task<Result<T>>> func, Func<T> cancellationValueFactory, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return await func();
        }
        catch (OperationCanceledException)
        {
            return Success(cancellationValueFactory());
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }

    public static async Task<Result<T>> TryCancelable<T>(Func<Task<Result<T>>> func, T cancellationDefaultValue, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return await func();
        }
        catch (OperationCanceledException)
        {
            return Success(cancellationDefaultValue);
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }

    // ============================================================
    // Group 3: Synchronous, raw value
    // ============================================================

    public static Result TryCancelable(Action action, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            action();
            return Success();
        }
        catch (OperationCanceledException)
        {
            return Success();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure(errorHandler(ex));
        }
    }

    public static Result<T> TryCancelable<T>(Func<T> func, Func<T> cancellationValueFactory, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return Success(func());
        }
        catch (OperationCanceledException)
        {
            return Success(cancellationValueFactory());
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }

    public static Result<T> TryCancelable<T>(Func<T> func, T cancellationDefaultValue, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return Success(func());
        }
        catch (OperationCanceledException)
        {
            return Success(cancellationDefaultValue);
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }

    // ============================================================
    // Group 4: Synchronous, Result-producing
    // ============================================================

    public static Result TryCancelable(Func<Result> func, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return func();
        }
        catch (OperationCanceledException)
        {
            return Success();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure(errorHandler(ex));
        }
    }

    public static Result<T> TryCancelable<T>(Func<Result<T>> func, Func<T> cancellationValueFactory, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return func();
        }
        catch (OperationCanceledException)
        {
            return Success(cancellationValueFactory());
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }

    public static Result<T> TryCancelable<T>(Func<Result<T>> func, T cancellationDefaultValue, Func<Exception, ResultError> errorHandler)
    {
        try
        {
            return func();
        }
        catch (OperationCanceledException)
        {
            return Success(cancellationDefaultValue);
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            return Failure<T>(errorHandler(ex));
        }
    }
}