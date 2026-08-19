using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces.Repositories;

namespace EventReservation.Application.Features.Seats.ReleaseSeat;

public sealed class ReleaseSeatCommandHandler(ISeatRepository seatRepository) : ICommandHandler<ReleaseSeatCommand, ReleaseSeatResult>
{
    public Task<Result<ReleaseSeatResult>> HandleAsync(ReleaseSeatCommand command, CancellationToken cancellationToken = default) =>
        seatRepository.GetByIdAsync(command.SeatId, cancellationToken)
            .Bind(seat => seat.Release())
            .Bind(seat => seatRepository.TryReleaseAsync(seat.Id, cancellationToken)
                .Bind(released => released
                    ? Success(new ReleaseSeatResult(seat.Id))
                    : Failure<ReleaseSeatResult>(ReleaseSeatErrors.SeatNotHeldOrReserved)));
}