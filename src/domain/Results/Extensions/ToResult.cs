namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    public static async Task<Result> ToResult(this Task<bool> conditionTask, ResultError error)
    {
        var condition = await conditionTask;
        return condition
            ? Success()
            : Failure(error);
    }

    public static async Task<Result<T>> ToResult<T>(this Task<T?> valueTask, ResultError error)
        where T : class
    {
        var value = await valueTask;
        return value is not null
            ? Success(value)
            : Failure<T>(error);
    }
}