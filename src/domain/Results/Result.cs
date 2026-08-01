using System.Diagnostics.CodeAnalysis;

namespace EventReservation.Domain.Results;

public record Result
{
    public bool IsSuccess { get; init; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyCollection<ResultError> Errors { get; init; } = Array.Empty<ResultError>();

    protected internal Result(bool isSuccess, IReadOnlyCollection<ResultError> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors ?? Array.Empty<ResultError>();
    }
}

public record Result<T> : Result
{
    public T? Value { get; init; }

    [MemberNotNullWhen(true, nameof(Value))]
    public new bool IsSuccess
    {
        get => base.IsSuccess;
        init => base.IsSuccess = value;
    }

    [MemberNotNullWhen(false, nameof(Value))]
    public new bool IsFailure => !IsSuccess;

    protected internal Result(T? value, bool isSuccess, IReadOnlyCollection<ResultError> errors)
        : base(isSuccess, errors)
    {
        Value = value;
    }
}