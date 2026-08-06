namespace EventReservation.Domain.Infrastructure;

public abstract class ResultConstructor<TModel>
{
    private readonly ResultErrors _errors = new();

    protected bool HasErrors => _errors.HasErrors;

    protected bool Require(bool condition, ResultError error) => _errors.Require(condition, error);

    protected void AddError(ResultError error) => _errors.AddError(error);

    protected void AddErrors(IEnumerable<ResultError> errors) => _errors.AddRange(errors);

    protected Result<TModel> ToFailureResult() => _errors.ToFailureResult<TModel>();

    public virtual Result<TModel> ExecuteSafely(Func<Result<TModel>> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception ex)
        {
            ResultTryExceptionHandling.RethrowIfCritical(ex);
            AddError(new ResultError("BUILD_ERROR", $"An unexpected error occurred while building the {typeof(TModel).Name} model."));
            return ToFailureResult();
        }
    }
}