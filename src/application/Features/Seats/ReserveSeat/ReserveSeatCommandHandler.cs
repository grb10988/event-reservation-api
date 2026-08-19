using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces.Repositories;

namespace EventReservation.Application.Features.Seats.ReserveSeat;

public sealed class ReserveSeatCommandHandler(ISeatRepository seatRepository) : ICommandHandler<ReserveSeatCommand, ReserveSeatResult>
{
    public Task<Result<ReserveSeatResult>> HandleAsync(ReserveSeatCommand command, CancellationToken cancellationToken = default) =>
        seatRepository.GetByIdAsync(command.SeatId, cancellationToken)
            .Bind(seat => seat.Reserve())
            .Bind(seat => seatRepository.TryReserveAsync(seat.Id, cancellationToken)
                .Bind(reserved => reserved
                    ? Success(new ReserveSeatResult(seat.Id))
                    : Failure<ReserveSeatResult>(ReserveSeatErrors.SeatNotHeld)));
}