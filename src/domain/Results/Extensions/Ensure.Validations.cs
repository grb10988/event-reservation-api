namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> Result, check via ResultErrors accumulator
    // ============================================================

    public static Result Ensure(this Result result, Func<ResultErrors> validations)
    {
        if (result.IsFailure)
            return result;

        var errors = validations();

        return errors.HasErrors
            ? Failure(errors.Errors)
            : result;
    }

    public static async Task<Result> Ensure(this Result result, Func<Task<ResultErrors>> validations)
    {
        if (result.IsFailure)
            return result;

        var errors = await validations();

        return errors.HasErrors
            ? Failure(errors.Errors)
            : result;
    }

    public static async Task<Result> Ensure(this Task<Result> resultTask, Func<ResultErrors> validations)
    {
        var result = await resultTask;
        return result.Ensure(validations);
    }

    public static async Task<Result> Ensure(this Task<Result> resultTask, Func<Task<ResultErrors>> validations)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return result;

        var errors = await validations();

        return errors.HasErrors
            ? Failure(errors.Errors)
            : result;
    }

    // ============================================================
    // Group 2: Result<T> -> Result<T>, check via ResultErrors accumulator
    // ============================================================

    public static Result<T> Ensure<T>(this Result<T> result, Func<T, ResultErrors> validations)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        var errors = validations(result.Value);

        return errors.HasErrors
            ? errors.ToFailureResult<T>()
            : result;
    }

    public static async Task<Result<T>> Ensure<T>(this Result<T> result, Func<T, Task<ResultErrors>> validations)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        var errors = await validations(result.Value);

        return errors.HasErrors
            ? errors.ToFailureResult<T>()
            : result;
    }

    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> resultTask, Func<T, ResultErrors> validations)
    {
        var result = await resultTask;
        return result.Ensure(validations);
    }

    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> resultTask, Func<T, Task<ResultErrors>> validations)
    {
        var result = await resultTask;

        if (result.IsFailure)
            return Failure<T>(result.Errors);

        var errors = await validations(result.Value);

        return errors.HasErrors
            ? errors.ToFailureResult<T>()
            : result;
    }
}