using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Orders;

public sealed record CreateOrderResult(
    Guid Id,
    Guid CustomerId,
    IReadOnlyCollection<Guid> ReservationIds,
    OrderStatus Status,
    DateTimeOffset CreatedAt);

public sealed record CreateOrderCommand(Guid CustomerId, IReadOnlyCollection<Guid> ReservationIds) : ICommand<CreateOrderResult>;

public sealed class CreateOrderCommandHandler(
    IOrderRepository orderRepository,
    IReservationRepository reservationRepository,
    TimeProvider timeProvider)
    : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    public async Task<Result<CreateOrderResult>> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        var reservationChecks = await Task.WhenAll(
            command.ReservationIds.Select(id => VerifyReservationAsync(id, command.CustomerId, cancellationToken)));

        var result = await Combine(reservationChecks)
            .Bind(() => Order.Create(command.CustomerId, command.ReservationIds, timeProvider))
            .Bind(order => orderRepository.AddAsync(order, cancellationToken))
            .Map(order => new CreateOrderResult(
                order.Id,
                order.CustomerId,
                order.ReservationIds,
                order.Status,
                order.CreatedAt));

        return result;
    }

    private async Task<Result> VerifyReservationAsync(Guid reservationId, Guid customerId, CancellationToken cancellationToken)
    {
        var result = await reservationRepository.GetByIdAsync(reservationId, cancellationToken)
            .Ensure(reservation =>
                reservation.CustomerId == customerId && reservation.Status == ReservationStatus.Held,
                Errors.ReservationNotOwnedOrHeld);

        return result;
    }

    public static class Errors
    {
        private const string Context = "CREATE_ORDER";
        public static ResultError ReservationNotOwnedOrHeld =>
            new(Context, "One or more reservations do not belong to this customer or are not currently held.");
    }
}