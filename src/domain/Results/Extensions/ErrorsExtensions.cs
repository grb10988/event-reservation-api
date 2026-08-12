using System.Diagnostics.CodeAnalysis;

namespace EventReservation.Domain.Results.Extensions;

public static class ResultErrorExtensions
{
    // ============================================================
    // Group 1: Accumulation
    // ============================================================

    public static void AddError(this ResultErrors errors, ResultError error) => errors.Add(error);

    public static void AddError(this ResultErrors errors, IEnumerable<ResultError> newErrors)
    {
        if (newErrors is not null)
            errors.AddRange(newErrors);
    }

    // ============================================================
    // Group 2: Condition checks
    // ============================================================

    public static bool Validate(this ResultErrors errors, bool condition, ResultError error)
    {
        if (condition)
            return true;

        errors.AddError(error);
        return false;
    }

    public static bool Validate(this ResultErrors errors, string? value, ResultError error)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return true;

        errors.AddError(error);
        return false;
    }

    public static bool Validate(this ResultErrors errors, Guid value, ResultError error)
    {
        if (value != Guid.Empty)
            return true;

        errors.AddError(error);
        return false;
    }

    public static bool Validate<T>(this ResultErrors errors, [NotNullWhen(true)] T? value, ResultError error)
       where T : class
    {
        if (value is not null)
            return true;

        errors.AddError(error);
        return false;
    }

    // ============================================================
    // Group 3: Conversion
    // ============================================================

    public static Result<T> ToFailureResult<T>(this ResultErrors errors) => Failure<T>(errors.Errors);
}