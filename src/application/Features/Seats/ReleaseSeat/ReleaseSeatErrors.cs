namespace EventReservation.Application.Features.Seats.ReleaseSeat;

public static class ReleaseSeatErrors
{
    private const string Context = "RELEASE_SEAT";
    public static ResultError SeatNotHeldOrReserved => new(Context, "The requested seat is not currently held or reserved and cannot be released.");
}