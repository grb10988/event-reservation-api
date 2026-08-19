namespace EventReservation.Domain.Construction;

public interface IModelBuilder<TModel>
{
    public Result<TModel> Build();
}

public abstract class ModelBuilder<TModel> : ResultConstructor<TModel>, IModelBuilder<TModel>
{
    public virtual Result<TModel> Build() => BuildResult(BuildInternal);
    protected abstract Result<TModel> BuildInternal();
}