using EventReservation.Application.Abstractions.Requests;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Orders;

public sealed record CreateOrderResult(
    Guid Id,
    Guid CustomerId,
    IReadOnlyCollection<Guid> ReservationIds,
    OrderStatus Status,
    DateTimeOffset CreatedAt);

public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyCollection<Guid> ReservationIds)
    : ICommand<CreateOrderResult>;

public sealed class CreateOrderCommandHandler(
    IOrderRepository orderRepository,
    IReservationRepository reservationRepository,
    TimeProvider timeProvider)
    : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    public async Task<Result<CreateOrderResult>> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var loaded = await Combine(command.ReservationIds
            .Select(id => reservationRepository.GetByIdAsync(id, cancellationToken))
            .ToArray());

        var result = await loaded
            .Ensure(reservations => reservations.All(r =>
                    r.CustomerId == command.CustomerId &&
                    r.Status == ReservationStatus.Held &&
                    r.HoldExpiresAt > now),
                Errors.ReservationNotOwnedOrHeld)
            .Ensure(reservations => reservations.Select(r => r.EventId).Distinct().Count() == 1,
                Errors.ReservationsSpanMultipleEvents)
            .Bind(_ => Order.Create(command.CustomerId, command.ReservationIds, timeProvider))
            .Bind(order => orderRepository.AddAsync(order, cancellationToken))
            .Map(order => new CreateOrderResult(
                order.Id,
                order.CustomerId,
                order.ReservationIds,
                order.Status,
                order.CreatedAt));

        return result;
    }

    public static class Errors
    {
        private const string Context = "CREATE_ORDER";

        public static ResultError ReservationNotOwnedOrHeld =>
            new(Context, "One or more reservations do not belong to this customer, are not currently held, or have expired.", ErrorCategory.Conflict);

        public static ResultError ReservationsSpanMultipleEvents =>
            new(Context, "All reservations in an order must belong to the same event.", ErrorCategory.Validation);
    }
}