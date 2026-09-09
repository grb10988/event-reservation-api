namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    private const string DefaultSeparator = "; ";

    public static IReadOnlyList<string> ToFailureMessages(this Result result) =>
        result.Errors.Select(e => e.Message).ToArray();

    public static IReadOnlyList<string> ToFailureMessages<T>(this Result<T> result) =>
        result.Errors.Select(e => e.Message).ToArray();

    public static IReadOnlyList<string> ToFailureMessages(this IEnumerable<ResultErrors> errors) =>
        errors.Select(e => e.ToString() ?? string.Empty).ToArray();

    public static string ToFailureMessages(this Result result, string separator = DefaultSeparator) =>
        result.Errors.ToFailureMessages(separator);

    public static string ToFailureMessages<T>(this Result<T> result, string separator = DefaultSeparator) =>
        result.Errors.ToFailureMessages(separator);

    public static string ToFailureMessages(this IEnumerable<ResultError> errors, string separator = DefaultSeparator) =>
        string.Join(separator, errors.Select(e => e.ToString()));
}