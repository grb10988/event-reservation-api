namespace EventReservation.Infrastructure.Persistence.Repositories;

internal static class RepositoryErrors
{
    private const string Context = "REPOSITORY";

    public static ResultError NotFound =>
        new(Context, "The requested record was not found.", ErrorCategory.NotFound);

    public static ResultError ReservationNoLongerAvailable =>
        new(Context, "One or more reservations are no longer held or have expired.", ErrorCategory.Conflict);
}