using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;

namespace EventReservation.Application.Features.Seats;

public sealed record ReserveSeatResult(Guid SeatId);
public sealed record ReserveSeatCommand(Guid SeatId) : ICommand<ReserveSeatResult>;

public sealed class ReserveSeatCommandHandler(ISeatRepository seatRepository)
    : ICommandHandler<ReserveSeatCommand, ReserveSeatResult>
{
    public Task<Result<ReserveSeatResult>> HandleAsync(
        ReserveSeatCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = seatRepository.GetByIdAsync(command.SeatId, cancellationToken)
            .Bind(seat => seat.Reserve())
            .Bind(seat => seatRepository.TryReserveAsync(seat.Id, cancellationToken)
                .Ensure(reserved => reserved, Errors.SeatNotHeld)
                .Map(_ => new ReserveSeatResult(seat.Id)));

        return result;
    }

    public static class Errors
    {
        private const string Context = "RESERVE_SEAT";
        public static ResultError SeatNotHeld => new(Context, "The requested seat is not currently held and cannot be reserved.");
    }
}
