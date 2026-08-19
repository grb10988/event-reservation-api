using EventReservation.Application.Abstractions;

namespace EventReservation.Application.Features.Seats.ReleaseSeat;

public sealed record ReleaseSeatCommand(Guid SeatId) : ICommand<ReleaseSeatResult>;