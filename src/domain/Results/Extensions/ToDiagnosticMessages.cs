namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    public static string ToDiagnosticMessages(this IEnumerable<ResultError> errors, string separator = DefaultSeparator) =>
        string.Join(separator, errors.Select(e => e.ToString()));

    public static string ToDiagnosticMessages(this Result result, string separator = DefaultSeparator) =>
        result.Errors.ToDiagnosticMessages(separator);

    public static string ToDiagnosticMessages<T>(this Result<T> result, string separator = DefaultSeparator) =>
        result.Errors.ToDiagnosticMessages(separator);
}