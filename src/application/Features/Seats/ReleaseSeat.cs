using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;

namespace EventReservation.Application.Features.Seats;

public sealed record ReleaseSeatResult(Guid SeatId);
public sealed record ReleaseSeatCommand(Guid SeatId) : ICommand<ReleaseSeatResult>;

public sealed class ReleaseSeatCommandHandler(ISeatRepository seatRepository)
    : ICommandHandler<ReleaseSeatCommand, ReleaseSeatResult>
{
    public Task<Result<ReleaseSeatResult>> HandleAsync(
        ReleaseSeatCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = seatRepository.GetByIdAsync(command.SeatId, cancellationToken)
            .Bind(seat => seat.Release())
            .Bind(seat => seatRepository.TryReleaseAsync(seat.Id, cancellationToken)
                .Ensure(released => released, Errors.SeatNotHeldOrReserved)
                .Map(_ => new ReleaseSeatResult(seat.Id)));

        return result;
    }

    public static class Errors
    {
        private const string Context = "RELEASE_SEAT";
        public static ResultError SeatNotHeldOrReserved => new(Context, "The requested seat is not currently held or reserved and cannot be released.");
    }
}
