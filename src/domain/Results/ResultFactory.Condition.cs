namespace EventReservation.Domain.Results;

public static partial class ResultFactory
{
    public static Result SuccessIf(bool condition, ResultError error) =>
        condition
            ? Success()
            : Failure(error);

    public static Result<T> SuccessIf<T>(bool condition, T value, ResultError error) =>
        condition
            ? Success(value)
            : Failure<T>(error);

    public static Result<T> SuccessIf<T>(bool condition, Func<T> value, ResultError error) =>
        condition
            ? Success(value())
            : Failure<T>(error);

    public static Result FailureIf(bool condition, ResultError error) =>
        SuccessIf(!condition, error);

    public static Result<T> FailureIf<T>(bool condition, T value, ResultError error) =>
        SuccessIf(!condition, value, error);

    public static Result<T> FailureIf<T>(bool condition, Func<T> value, ResultError error) =>
        SuccessIf(!condition, value, error);
}