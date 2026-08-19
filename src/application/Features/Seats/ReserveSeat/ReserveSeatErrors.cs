namespace EventReservation.Application.Features.Seats.ReserveSeat;

public static class ReserveSeatErrors
{
    private const string Context = "RESERVE_SEAT";
    public static ResultError SeatNotHeld => new(Context, "The requested seat is not currently held and cannot be reserved.");
}