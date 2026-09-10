using OpenTelemetry;
using OpenTelemetry.Logs;

namespace EventReservation.Api.Diagnostics;

public sealed class SensitiveLogRedactingProcessor : BaseProcessor<LogRecord>
{
    private const string RedactedValue = "***REDACTED***";

    public override void OnEnd(LogRecord record)
    {
        if (record.Attributes is not { Count: > 0 } attributes)
            return;

        var redacted = new List<KeyValuePair<string, object?>>(attributes.Count);
        var changed = false;

        foreach (var attribute in attributes)
            if (IsSensitiveKey(attribute.Key) || (attribute.Value is string text && LooksLikeConnectionString(text)))
            {
                redacted.Add(new KeyValuePair<string, object?>(attribute.Key, RedactedValue));
                changed = true;
            }
            else
                redacted.Add(attribute);

        if (changed)
            record.Attributes = redacted;
    }

    private static bool IsSensitiveKey(string key) =>
        key.Equals("ConnectionString", StringComparison.OrdinalIgnoreCase)
        || key.Contains("password", StringComparison.OrdinalIgnoreCase)
        || key.Contains("authorization", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeConnectionString(string value) =>
        value.Contains("Password=", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Pwd=", StringComparison.OrdinalIgnoreCase);
}