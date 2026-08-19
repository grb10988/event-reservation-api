using EventReservation.Application.Abstractions;

namespace EventReservation.Application.Features.Seats.HoldSeat;

public sealed record HoldSeatCommand(Guid SeatId) : ICommand<HoldSeatResult>;