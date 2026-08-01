namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    public static Result Ensure(this Result result, Func<Result> func)
    {
        if (result.IsFailure)
            return result;

        var checkResult = func();

        return checkResult.IsSuccess
            ? result
            : Failure(checkResult.Errors);
    }

    public static Result Ensure(this Result result, Func<bool> predicate, ResultError error)
    {
        if (result.IsFailure)
            return result;

        return predicate()
            ? result
            : Failure(error);
    }

    public static Result Ensure(this Result result, Func<ResultErrors> validations)
    {
        if (result.IsFailure)
            return result;

        var errors = validations();

        return errors.HasErrors
            ? Failure(errors.Errors)
            : result;
    }

    public static Result<T> Ensure<T>(this Result<T> result, Func<T, Result> func)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        var checkResult = func(result.Value);

        return checkResult.IsSuccess
            ? result
            : Failure<T>(checkResult.Errors);
    }

    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, ResultError error)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        return predicate(result.Value)
            ? result
            : Failure<T>(error);
    }

    public static Result<T> Ensure<T>(this Result<T> result, Func<T, ResultErrors> validations)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        var errors = validations(result.Value);

        return errors.HasErrors
            ? errors.ToFailureResult<T>()
            : result;
    }
}