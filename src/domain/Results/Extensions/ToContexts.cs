namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    public static IReadOnlyList<string> ToContexts(this IEnumerable<ResultError> errors) =>
        errors.Select(e => e.Context).Distinct().ToArray();

    public static IReadOnlyList<string> ToContexts(this Result result) =>
        result.Errors.ToContexts();

    public static IReadOnlyList<string> ToContexts<T>(this Result<T> result) =>
        result.Errors.ToContexts();
}