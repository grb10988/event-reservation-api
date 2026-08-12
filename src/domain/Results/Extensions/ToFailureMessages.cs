namespace EventReservation.Domain.Results.Extensions;

public static partial class ResultExtensions
{
    public static IReadOnlyList<string> ToFailureMessages(this Result result) =>
        result.Errors.Select(e => e.Message).ToArray();
}