using System.Runtime.ExceptionServices;

namespace EventReservation.Domain.Results.Extensions;

internal static class ResultTryExceptionHandling
{
    public static void RethrowIfCritical(Exception ex)
    {
        if (ex is OperationCanceledException
            or ArgumentException
            or NullReferenceException
            or NotImplementedException
            or NotSupportedException)
            ExceptionDispatchInfo.Capture(ex).Throw();
    }
}