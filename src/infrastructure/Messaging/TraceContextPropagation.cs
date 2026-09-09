using System.Diagnostics;
using System.Text;

namespace EventReservation.Infrastructure.Messaging;

public static class TraceContextPropagation
{
    private const string TraceParentHeader = "traceparent";
    private const string TraceStateHeader = "tracestate";

    public static IDictionary<string, object?> InjectTraceContext(IDictionary<string, object?> headers, Activity? activity)
    {
        if (activity is null)
            return headers;

        headers[TraceParentHeader] = activity.Id;

        if (!string.IsNullOrEmpty(activity.TraceStateString))
            headers[TraceStateHeader] = activity.TraceStateString;

        return headers;
    }

    public static Result<ActivityContext> ExtractTraceContext(IDictionary<string, object?>? headers)
    {
        var result = Success()
            .Bind(() => ExtractTraceParentAndState(headers))
            .Bind(tp => ParseActivityContext(tp.Parent, tp.State));

        return result;
    }

    private static Result<(string Parent, string? State)> ExtractTraceParentAndState(IDictionary<string, object?>? headers)
    {
        if (headers is not { } validHeaders)
            return Failure<(string Parent, string? State)>(Errors.NullHeaders);

        var parent = GetHeaderValue(validHeaders, TraceParentHeader);
        var state = GetHeaderValue(validHeaders, TraceStateHeader);

        if (parent is not { } validParent)
            return Failure<(string Parent, string? State)>(Errors.MissingTraceParent);

        return Success((validParent, state));
    }

    private static Result<ActivityContext> ParseActivityContext(string parent, string? state)
    {
        var result = ActivityContext.TryParse(parent, state, out var context)
            ? Success(context)
            : Failure<ActivityContext>(Errors.InvalidTraceParent);

        return result;
    }

    private static string? GetHeaderValue(IDictionary<string, object?> headers, string key)
    {
        var value = headers.TryGetValue(key, out var val) switch
        {
            true when val is byte[] bytes => Encoding.UTF8.GetString(bytes),
            true when val is string str => str,
            _ => null
        };

        return value;
    }

    public static class Errors
    {
        private const string Context = "MESSAGING";

        public static ResultError NullHeaders => new(Context, "Header collection is null.", ErrorCategory.Validation);
        public static ResultError MissingTraceParent => new(Context, "Missing traceparent header.", ErrorCategory.NotFound);
        public static ResultError InvalidTraceParent => new(Context, "Failed to parse W3C traceparent header.", ErrorCategory.Validation);
    }
}