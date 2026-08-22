using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Orders;

public sealed record GetOrderByConfirmationNumberResult(
    Guid Id,
    Guid CustomerId,
    IReadOnlyCollection<Guid> ReservationIds,
    OrderStatus Status,
    string? ConfirmationNumber,
    DateTimeOffset CreatedAt);

public sealed record GetOrderByConfirmationNumberQuery(string ConfirmationNumber) : IQuery<GetOrderByConfirmationNumberResult>;

public sealed class GetOrderByConfirmationNumberQueryHandler(IOrderRepository orderRepository)
    : IQueryHandler<GetOrderByConfirmationNumberQuery, GetOrderByConfirmationNumberResult>
{
    public Task<Result<GetOrderByConfirmationNumberResult>> HandleAsync(
        GetOrderByConfirmationNumberQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = Success()
            .Ensure(() => Order.IsValidConfirmationNumberFormat(query.ConfirmationNumber), Errors.InvalidFormat)
            .Bind(() => orderRepository.GetByConfirmationNumberAsync(query.ConfirmationNumber, cancellationToken))
            .Map(o => new GetOrderByConfirmationNumberResult(
                o.Id,
                o.CustomerId,
                o.ReservationIds,
                o.Status,
                o.ConfirmationNumber,
                o.CreatedAt));

        return result;
    }

    public static class Errors
    {
        private const string Context = "GET_ORDER_BY_CONFIRMATION_NUMBER";
        public static ResultError InvalidFormat => new(Context, "The confirmation number is not in a valid format.");
    }
}