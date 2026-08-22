using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Orders;

public sealed record GetOrderByIdResult(
    Guid Id,
    Guid CustomerId,
    IReadOnlyCollection<Guid> ReservationIds,
    OrderStatus Status,
    string? ConfirmationNumber,
    DateTimeOffset CreatedAt);

public sealed record GetOrderByIdQuery(Guid Id) : IQuery<GetOrderByIdResult>;

public sealed class GetOrderByIdQueryHandler(IOrderRepository orderRepository)
    : IQueryHandler<GetOrderByIdQuery, GetOrderByIdResult>
{
    public Task<Result<GetOrderByIdResult>> HandleAsync(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = orderRepository.GetByIdAsync(query.Id, cancellationToken)
            .Map(o => new GetOrderByIdResult(
                o.Id,
                o.CustomerId,
                o.ReservationIds,
                o.Status,
                o.ConfirmationNumber,
                o.CreatedAt));

        return result;
    }
}