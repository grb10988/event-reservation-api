namespace EventReservation.Application.Abstractions.Requests;

public interface IRequest<TResponse> { }

public interface ICommand<TResponse> : IRequest<TResponse> { }

public interface IQuery<TResponse> : IRequest<TResponse> { }

public delegate Task<Result<TResponse>> RequestHandlerDelegate<TResponse>();