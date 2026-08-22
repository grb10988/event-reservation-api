using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;

namespace EventReservation.Application.Features.Seats;

public sealed record HoldSeatResult(Guid SeatId);
public sealed record HoldSeatCommand(Guid SeatId) : ICommand<HoldSeatResult>;

public sealed class HoldSeatCommandHandler(ISeatRepository seatRepository)
    : ICommandHandler<HoldSeatCommand, HoldSeatResult>
{
    public Task<Result<HoldSeatResult>> HandleAsync(
        HoldSeatCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = seatRepository.GetByIdAsync(command.SeatId, cancellationToken)
            .Bind(seat => seat.Hold())
            .Bind(seat => seatRepository.TryHoldAsync(seat.Id, cancellationToken)
                .Ensure(held => held, Errors.SeatNotAvailable)
                .Map(_ => new HoldSeatResult(command.SeatId)));

        return result;
    }

    public static class Errors
    {
        private const string Context = "HOLD_SEAT";
        public static ResultError SeatNotAvailable => new(Context, "The requested seat is not available to hold.");
    }
}
