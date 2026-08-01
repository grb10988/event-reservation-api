namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    public static Result Combine(params Result[] results)
    {
        var errors = new ResultErrors();

        foreach (var result in results)
            if (result.IsFailure)
                errors.AddError(result.Errors);

        return errors.HasErrors
            ? Failure(errors.Errors)
            : Success();
    }

    public static Result<T[]> Combine<T>(params Result<T>[] results)
    {
        var errors = new ResultErrors();
        var values = new List<T>(results.Length);

        foreach (var result in results)
            if (result.IsFailure)
                errors.AddError(result.Errors);

        return errors.HasErrors
            ? Failure<T[]>(errors.Errors)
            : Success(values.ToArray());
    }
}