using System.Diagnostics.CodeAnalysis;

namespace EventReservation.Domain.Construction;

public abstract class ResultConstructor<TModel>
{
    private protected ResultConstructor() { }

    protected bool HasErrors => _errors.HasErrors;

    private readonly ResultErrors _errors = new();
    protected void AddError(ResultError error) => _errors.Add(error);
    protected void AddErrors(IEnumerable<ResultError> errors) => _errors.AddRange(errors);

    protected bool Validate(bool condition, ResultError error) => _errors.Validate(condition, error);
    protected bool Validate<T>([NotNullWhen(true)] T? value, ResultError error) where T : class => _errors.Validate(value, error);
    protected bool Validate<T>([NotNullWhen(true)] T? value, ResultError error) where T : struct => _errors.Validate(value, error);
    protected bool Validate([NotNullWhen(true)] string? value, ResultError error) => _errors.Validate(value, error);
    protected bool Validate(Guid value, ResultError error) => _errors.Validate(value, error);

    protected Result<TModel> ToFailureResult() => _errors.ToFailureResult<TModel>();

    private Result<TModel> Attempt(Func<Result<TModel>> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            TryExceptionHandler.RethrowIfCritical(ex);
            AddError(new ResultError("BUILD_ERROR", $"An unexpected error occurred while building the {typeof(TModel).Name} model."));
            return ToFailureResult();
        }
    }

    protected virtual Result<TModel> BuildResult(Func<Result<TModel>> operation) => Attempt(operation);
    protected virtual Result<TModel> CreateResult(Func<Result<TModel>> operation) => Attempt(operation);
}