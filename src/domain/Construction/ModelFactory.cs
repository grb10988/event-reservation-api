namespace EventReservation.Domain.Construction;

public abstract class ModelFactory<TModel> : ResultConstructor<TModel>
{
    public Result<TModel> Create() => CreateResult(CreateInternal);
    protected abstract Result<TModel> CreateInternal();
}