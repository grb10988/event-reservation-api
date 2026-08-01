namespace EventReservation.Domain.Results;

public static class ResultFactory
{
    public static Result Success() => new(isSuccess: true, errors: []);
    public static Result<T> Success<T>(T value) => new(value, isSuccess: true, errors: []);

    public static Result Failure(ResultError error) => new(isSuccess: false, errors: [error]);
    public static Result<T> Failure<T>(ResultError error) => new(default, isSuccess: false, errors: [error]);

    public static Result Failure(IReadOnlyCollection<ResultError> errors) => new(isSuccess: false, errors: errors);
    public static Result<T> Failure<T>(IReadOnlyCollection<ResultError> errors) => new(default, isSuccess: false, errors: errors);
}