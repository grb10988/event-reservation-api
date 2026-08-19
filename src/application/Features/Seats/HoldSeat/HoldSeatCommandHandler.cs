using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces.Repositories;

namespace EventReservation.Application.Features.Seats.HoldSeat;

public sealed class HoldSeatCommandHandler(ISeatRepository seatRepository) : ICommandHandler<HoldSeatCommand, HoldSeatResult>
{
    public Task<Result<HoldSeatResult>> HandleAsync(HoldSeatCommand command, CancellationToken cancellationToken = default) =>
        seatRepository.GetByIdAsync(command.SeatId, cancellationToken)
            .Bind(seat => seat.Hold())
            .Bind(seat => seatRepository.TryHoldAsync(seat.Id, cancellationToken)
                .Bind(held => held
                    ? Success(new HoldSeatResult(command.SeatId))
                    : Failure<HoldSeatResult>(HoldSeatErrors.SeatNotAvailable)));
}