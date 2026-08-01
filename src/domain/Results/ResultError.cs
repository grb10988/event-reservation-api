using System.Diagnostics.CodeAnalysis;

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

    public void AddError(ResultError error)
    {
        _errors ??= [];
        _errors.Add(error);
    }

    public void AddError(IEnumerable<ResultError> errors)
    {
        if (errors is null)
            return;

        _errors ??=[];

        if (errors is ICollection<ResultError> collection)
            _errors.AddRange(collection);
        else
            foreach (var error in errors)
                AddError(error);
    }

    public bool Require<T>([NotNullWhen(true)] T? value, ResultError error)
        where T : class
    {
        if (value is not null)
            return true;

        AddError(error);
        return false;
    }

    public bool Require(bool condition, ResultError error)
    {
        if (condition)
            return true;

        AddError(error);
        return false;
    }

    public void Clear()
    {
        _errors?.Clear();
    }

    public Result<T> ToFailureResult<T>() => Failure<T>(Errors);
}