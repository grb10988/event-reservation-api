namespace EventReservation.Domain.Results;

public readonly record struct ResultError(string Context, string Message)
{
    public override string ToString() => $"[{Context}] {Message}";
}

public sealed class ResultErrors
{
    private List<ResultError>? _errors;

    public bool HasErrors => _errors is { Count: > 0 };
    public IReadOnlyCollection<ResultError> Errors => _errors != null
        ? _errors
        : Array.Empty<ResultError>();

    internal void Add(ResultError error) => (_errors ??= []).Add(error);
    internal void AddRange(IEnumerable<ResultError> errors) => (_errors ??= []).AddRange(errors);

    public void Clear() => _errors?.Clear();
}