using System.Diagnostics;
using OpenTelemetry;

namespace EventReservation.Api.Diagnostics;

public sealed class SensitiveActivityRedactingProcessor : BaseProcessor<Activity>
{
    private const string RedactedValue = "***REDACTED***";

    // Exact attribute keys that must never leave the process, regardless of value.
    // "db.statement" is blocked outright here as the safer default - even though
    // this project's parameterized queries mean it shouldn't contain literal PII
    // today, blocking it removes the risk entirely rather than trusting that
    // every future query stays parameterized.
    private static readonly HashSet<string> BlockedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "db.connection_string",
        "db.statement",
        "http.request.header.authorization",
        "http.request.header.cookie"
    };

    public override void OnEnd(Activity activity)
    {
        foreach (var key in BlockedKeys)
            if (activity.GetTagItem(key) is not null)
                activity.SetTag(key, RedactedValue);

        // Defense-in-depth: catch a secret under an unexpected key name -
        // e.g. someone's future debugging code tagging a raw connection string
        foreach (var tag in activity.TagObjects.ToList())
            if (tag.Value is string text && LooksLikeConnectionString(text))
                activity.SetTag(tag.Key, RedactedValue);
    }

    private static bool LooksLikeConnectionString(string value) =>
        value.Contains("Password=", StringComparison.OrdinalIgnoreCase)
        || value.Contains("Pwd=", StringComparison.OrdinalIgnoreCase);
}