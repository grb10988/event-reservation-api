using System.Diagnostics;
using EventReservation.Application.Abstractions;
using EventReservation.Application.Behaviors;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventReservation.Infrastructure.BackgroundServices;

public sealed class ReservationHoldSweepWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<ReservationHoldSweepSettings> settings,
    ILogger<ReservationHoldSweepWorker> logger) : BackgroundService
{
    private readonly ReservationHoldSweepSettings _settings = settings.Value;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_settings.Interval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
            await RunSweepTickAsync(cancellationToken);
    }

    private async Task RunSweepTickAsync(CancellationToken cancellationToken)
    {
        const string name = "ReservationHoldSweep";
        var stopwatch = Stopwatch.StartNew();

        PipelineLogging.LogStart(logger, name);

        try
        {
            var result = await SweepOnceAsync(cancellationToken);
            stopwatch.Stop();
            PipelineLogging.LogCompleted(logger, name, result, stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            logger.LogCritical(
                ex,
                "Unexpected, unhandled exception running {Name} after {ElapsedMilliseconds:F2}ms",
                name,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private async Task<Result> SweepOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var reservationRepository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var result = await reservationRepository.GetExpiredHoldsAsync(timeProvider.GetUtcNow(), cancellationToken)
            .Bind(expired => DispatchExpirationsAsync(expired, dispatcher, cancellationToken));

        return result;
    }

    private static async Task<Result> DispatchExpirationsAsync(
        IReadOnlyList<Reservation> expired,
        IDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var outcomes = await Task.WhenAll(expired.Select(reservation =>
            dispatcher.SendAsync(new ExpireReservationCommand(reservation.Id), cancellationToken)));

        // Deliberately not Combine(outcomes): each dispatch already gets its
        // own trace/log/metric via the request pipeline, and a reservation
        // losing its race to a Confirm moments before the sweep reached it is
        // a routine, expected outcome there - not a failure of this sweep.
        // this tick only fails if GetExpiredHoldAsync itself fails above.

        return Success();
    }
}