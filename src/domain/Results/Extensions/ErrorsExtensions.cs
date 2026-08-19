using System.Diagnostics.CodeAnalysis;

namespace EventReservation.Domain.Results.Extensions;

public static class ResultErrorExtensions
{
    /// <summary>
    /// Records <paramref name="error"/> unless <paramref name="condition"/> holds.
    /// Returns the condition so callers can short-ciruit dependent checks.
    /// </summary>
    /// <param name="errors"></param>
    /// <param name="condition"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public static bool Validate(this ResultErrors errors, bool condition, ResultError error)
    {
        if (condition)
            return true;

        errors.Add(error);
        return false;
    }

    /// <summary>
    /// Records <paramref name="error"/> when <paramref name="value"/> is null.
    /// Flows non-null on success so callers avoid the null-forgiving operator.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="errors"></param>
    /// <param name="value"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public static bool Validate<T>(this ResultErrors errors, [NotNullWhen(true)] T? value, ResultError error)
        where T : class
    {
        if (value is not null)
            return true;

        errors.Add(error);
        return false;
    }

    /// <summary>
    /// Records <paramref name="error"/> when <paramref name="value"/> has no value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="errors"></param>
    /// <param name="value"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public static bool Validate<T>(this ResultErrors errors, [NotNullWhen(true)] T? value, ResultError error)
        where T : struct
    {
        if (value.HasValue)
            return true;

        errors.Add(error);
        return false;
    }

    /// <summary>
    /// Records <paramref name="error"/> when <paramref name="value"/> is null, empty, or whitespace.
    /// </summary>
    /// <param name="errors"></param>
    /// <param name="value"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public static bool Validate(this ResultErrors errors, [NotNullWhen(true)] string? value, ResultError error)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return true;

        errors.Add(error);
        return false;
    }

    /// <summary>
    /// Records <paramref name="error"/> when <paramref name="value"/> is an empty Guid.
    /// </summary>
    /// <param name="errors"></param>
    /// <param name="value"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public static bool Validate(this ResultErrors errors, Guid value, ResultError error)
    {
        if (value != Guid.Empty)
            return true;

        errors.Add(error);
        return false;
    }

    public static Result ToFailureResult(this ResultErrors errors) => Failure(errors.Errors);
    public static Result<T> ToFailureResult<T>(this ResultErrors errors) => Failure<T>(errors.Errors);
}