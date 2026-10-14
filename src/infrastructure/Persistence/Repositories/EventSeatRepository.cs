using Dapper;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed class EventSeatRow
{
    public Guid EventId { get; init; }
    public Guid SeatId { get; init; }
    public EventSeatStatus Status { get; init; }
    public Guid? HeldBy { get; init; }
}

public sealed class EventSeatRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<EventSeatRepository> logger) : IEventSeatRepository
{
    public Task<Result<EventSeat>> GetByIdAsync(
        Guid seatId,
        Guid eventId,
        CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rows = await connection.QueryAsync<EventSeatRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        es.event_id AS EventId,
                        es.seat_id AS SeatId,
                        es.status AS Status,
                        es.held_by AS HeldBy
                    FROM
                        event_seats AS es
                    WHERE
                        es.seat_id = @SeatId AND
                        es.event_id = @EventId
                    ",
                    new { SeatId = seatId, EventId = eventId },
                    cancellationToken: cancellationToken));

            return rows.ToList();
        })
        .Bind(rows => rows.Count == 0
            ? Failure<EventSeat>(RepositoryErrors.NotFound)
            : Success(EventSeat.Rehydrate(
                rows[0].EventId,
                rows[0].SeatId,
                rows[0].Status,
                rows[0].HeldBy)));

    public Task<Result<IReadOnlyList<EventSeat>>> GetByEventIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async Task<IReadOnlyList<EventSeat>> (connection) =>
        {
            var rows = await connection.QueryAsync<EventSeatRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        es.event_id AS EventId,
                        es.seat_id AS SeatId,
                        es.status AS Status,
                        es.held_by AS HeldBy
                    FROM
                        event_seats AS es
                    JOIN
                        seats AS s ON s.id = es.seat_id
                    WHERE
                        es.event_id = @EventId
                    ORDER BY
                        s.section,
                        s.row,
                        s.number
                    ",
                    new { EventId = eventId },
                    cancellationToken: cancellationToken));

            return rows.Select(r => EventSeat.Rehydrate(
                r.EventId,
                r.SeatId,
                r.Status,
                r.HeldBy)).ToList();
        });

    public Task<Result> ProvisionAsync(
        Guid eventId,
        Guid venueId,
        CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    INSERT INTO event_seats
                    (
                        event_id,
                        seat_id,
                        status
                    )
                    SELECT
                        @EventId,
                        s.id,
                        @AvailableStatus
                    FROM
                        seats AS s
                    WHERE
                        s.venue_id = @VenueId
                    ON CONFLICT (seat_id, event_id) DO NOTHING
                    ",
                    new
                    {
                        EventId = eventId,
                        VenueId = venueId,
                        AvailableStatus = EventSeatStatus.Available.ToString()
                    },
                    cancellationToken: cancellationToken)))
        .Bind(_ => Success());


    public Task<Result<bool>> TryHoldAsync(
        Guid seatId,
        Guid eventId,
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        event_seats AS es
                    SET
                        status = @NewStatus,
                        held_by = @ReservationId
                    FROM
                        events AS e
                    WHERE
                        es.event_id = e.id AND
                        es.seat_id = @SeatId AND
                        es.event_id = @EventId AND
                        es.status = @RequiredStatus AND
                        e.status = @PublishedStatus
                    ",
                    new
                    {
                        SeatId = seatId,
                        EventId = eventId,
                        ReservationId = reservationId,
                        NewStatus = EventSeatStatus.Held.ToString(),
                        RequiredStatus = EventSeatStatus.Available.ToString(),
                        PublishedStatus = EventStatus.Published.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        });

    public Task<Result<bool>> TryReserveAsync(
        Guid seatId,
        Guid eventId,
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        event_seats
                    SET
                        status = @NewStatus
                    WHERE
                        seat_id = @SeatId AND
                        event_id = @EventId AND
                        status = @RequiredStatus AND
                        held_by = @ReservationId
                    ",
                    new
                    {
                        SeatId = seatId,
                        EventId = eventId,
                        ReservationId = reservationId,
                        NewStatus = EventSeatStatus.Reserved.ToString(),
                        RequiredStatus = EventSeatStatus.Held.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        });

    public Task<Result<bool>> TryReleaseAsync(
        Guid seatId,
        Guid eventId,
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        event_seats
                    SET
                        status = @NewStatus,
                        held_by = NULL
                    WHERE
                        seat_id = @SeatId AND
                        event_id = @EventId AND
                        held_by = @ReservationId
                    ",
                    new
                    {
                        SeatId = seatId,
                        EventId = eventId,
                        ReservationId = reservationId,
                        NewStatus = EventSeatStatus.Available.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        });
}