using EventReservation.Application.Abstractions;

namespace EventReservation.Application.Features.Seats.ReserveSeat;

public sealed record ReserveSeatCommand(Guid SeatId) : ICommand<ReserveSeatResult>;