namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> TResult, Match (branch on success/failure)
    // ============================================================

    public static TResult Match<TResult>(this Result result, Func<TResult> onSuccess, Func<IReadOnlyCollection<ResultError>, TResult> onFailure)
        => result.IsSuccess
            ? onSuccess()
            : onFailure(result.Errors);

    public static async Task<TResult> Match<TResult>(this Task<Result> resultTask, Func<TResult> onSuccess, Func<IReadOnlyCollection<ResultError>, TResult> onFailure)
    {
        var result = await resultTask;
        return result.Match(onSuccess, onFailure);
    }

    // ============================================================
    // Group 2: Result<T> -> TResult, Match (branch on success/failure)
    // ============================================================

    public static TResult Match<T, TResult>(this Result<T> result, Func<T, TResult> onSuccess, Func<IReadOnlyCollection<ResultError>, TResult> onFailure)
        => result.IsSuccess
            ? onSuccess(result.Value)
            : onFailure(result.Errors);

    public static async Task<TResult> Match<T, TResult>(this Task<Result<T>> resultTask, Func<T, TResult> onSuccess, Func<IReadOnlyCollection<ResultError>, TResult> onFailure)
    {
        var result = await resultTask;
        return result.Match(onSuccess, onFailure);
    }
}