using EventReservation.Application.Abstractions.Requests;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Orders;

public sealed record OrderSummary(Guid Id, OrderStatus Status, string? ConfirmationNumber, DateTimeOffset CreatedAt);
public sealed record GetOrdersByCustomerIdQuery(Guid CustomerId) : IQuery<IReadOnlyList<OrderSummary>>;

public sealed class GetOrdersByCustomerIdQueryHandler(IOrderRepository orderRepository)
    : IQueryHandler<GetOrdersByCustomerIdQuery, IReadOnlyList<OrderSummary>>
{
    public Task<Result<IReadOnlyList<OrderSummary>>> HandleAsync(
        GetOrdersByCustomerIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = orderRepository.GetByCustomerIdAsync(query.CustomerId, cancellationToken)
            .Map(orders => (IReadOnlyList<OrderSummary>)orders
                .Select(o => new OrderSummary(o.Id, o.Status, o.ConfirmationNumber, o.CreatedAt))
                .ToList());

        return result;
    }
}