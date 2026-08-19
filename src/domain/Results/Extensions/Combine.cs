namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    // ============================================================
    // Group 1: Result[] -> Result
    // ============================================================

    public static Result Combine(params Result[] results)
    {
        var errors = new ResultErrors();

        foreach (var result in results)
            if (result.IsFailure)
                errors.AddRange(result.Errors);

        return errors.HasErrors
            ? Failure(errors.Errors)
            : Success();
    }

    public static async Task<Result> Combine(params Task<Result>[] resultTasks)
    {
        var results = await Task.WhenAll(resultTasks);
        return Combine(results);
    }

    // ============================================================
    // Group 2: Result<T>[] -> Result<T[]>
    // ============================================================

    public static Result<T[]> Combine<T>(params Result<T>[] results)
    {
        var errors = new ResultErrors();
        var values = new List<T>(results.Length);

        foreach (var result in results)
            if (result.IsSuccess)
                values.Add(result.Value);
            else
                errors.AddRange(result.Errors);

        return errors.HasErrors
            ? Failure<T[]>(errors.Errors)
            : Success(values.ToArray());
    }

    public static async Task<Result<T[]>> Combine<T>(params Task<Result<T>>[] resultTasks)
    {
        var results = await Task.WhenAll(resultTasks);
        return Combine(results);
    }
}