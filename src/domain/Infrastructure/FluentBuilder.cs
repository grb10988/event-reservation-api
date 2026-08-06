namespace EventReservation.Domain.Infrastructure;

public interface IFluentBuilder<TModel>
{
    public Result<TModel> Build();
}

public abstract class FluentBuilder<TModel> : ResultConstructor<TModel>, IFluentBuilder<TModel>
{
    public virtual Result<TModel> Build() => ExecuteSafely(BuildInternal);

    protected abstract Result<TModel> BuildInternal();
}