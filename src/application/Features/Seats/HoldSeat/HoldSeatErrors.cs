namespace EventReservation.Application.Features.Seats.HoldSeat;

public static class HoldSeatErrors
{
    private const string Context = "HOLD_SEAT";
    public static ResultError SeatNotAvailable => new(Context, "The requested seat is not available to hold.");
}