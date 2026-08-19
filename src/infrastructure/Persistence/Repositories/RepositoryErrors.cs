namespace EventReservation.Infrastructure.Persistence.Repositories;

internal static class RepositoryErrors
{
    private const string Context = "REPOSITORY";
    public static ResultError NotFound => new(Context, "The requested record was not found.");
}