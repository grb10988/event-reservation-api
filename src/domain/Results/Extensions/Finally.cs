namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result -> TResult, Finally (whole result exposed)
    // ============================================================

    public static TResult Finally<TResult>(this Result result, Func<Result, TResult> func)
        => func(result);

    public static async Task<TResult> Finally<TResult>(this Task<Result> resultTask, Func<Result, TResult> func)
    {
        var result = await resultTask;
        return func(result);
    }

    // ============================================================
    // Group 2: Result<T> -> TResult, Finally (whole result exposed)
    // ============================================================

    public static TResult Finally<T, TResult>(this Result<T> result, Func<Result<T>, TResult> func)
        => func(result);

    public static async Task<TResult> Finally<T, TResult>(this Task<Result<T>> resultTask, Func<Result<T>, TResult> func)
    {
        var result = await resultTask;
        return func(result);
    }
}