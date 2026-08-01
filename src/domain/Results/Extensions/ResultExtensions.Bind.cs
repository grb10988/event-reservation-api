namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    public static Result Bind(this Result result, Func<Result> func)
    {
        if (result.IsFailure)
            return result;

        return func();
    }

    public static Result Bind<T>(this Result<T> result, Func<Result> func)
    {
        if (result.IsFailure)
            return Failure(result.Errors);

        return func();
    }

    public static Result<T> Bind<T>(this Result result, Func<Result<T>> func)
    {
        if (result.IsFailure)
            return Failure<T>(result.Errors);

        return func();
    }

    public static Result<TNew> Bind<TOld, TNew>(this Result<TOld> result, Func<TOld, Result<TNew>> func)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        return func(result.Value);
    }

    public static Result<TNew> Bind<TOld, TNew>(this Result<TOld> result, Func<Result<TNew>> func)
    {
        if (result.IsFailure)
            return Failure<TNew>(result.Errors);

        return func();
    }
}