using System.Runtime.ExceptionServices;

namespace EventReservation.Domain.Results.Extensions;

internal static class TryExceptionHandler
{
    public static void RethrowIfCritical(Exception ex)
    {
        if (ex is OperationCanceledException
            or ArgumentException
            or NullReferenceException
            or NotImplementedException
            or NotSupportedException
            or InvalidOperationException)
            ExceptionDispatchInfo.Capture(ex).Throw();
    }
}