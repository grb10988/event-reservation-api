using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Orders;

public sealed record OrderSummary(Guid Id, OrderStatus Status, string? ConfirmationNumber, DateTimeOffset CreatedAt);
public sealed record GetOrdersByCustomerQuery(Guid CustomerId) : IQuery<IReadOnlyList<OrderSummary>>;

public sealed class GetOrdersByCustomerQueryHandler(IOrderRepository orderRepository)
    : IQueryHandler<GetOrdersByCustomerQuery, IReadOnlyList<OrderSummary>>
{
    public Task<Result<IReadOnlyList<OrderSummary>>> HandleAsync(GetOrdersByCustomerQuery query, CancellationToken cancellationToken = default)
    {
        var result = orderRepository.GetByCustomerIdAsync(query.CustomerId, cancellationToken)
            .Map(orders => (IReadOnlyList<OrderSummary>)orders
                .Select(o => new OrderSummary(o.Id, o.Status, o.ConfirmationNumber, o.CreatedAt))
                .ToList());

        return result;
    }
}