namespace EventReservation.Infrastructure.BackgroundServices;

public sealed class ReservationHoldSweepSettings
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);
}