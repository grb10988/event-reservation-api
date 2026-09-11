using Dapper;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed class EventRow
{
    public Guid Id { get; init; }
    public Guid VenueId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public decimal TicketPrice { get; init; }
    public EventStatus Status { get; init; }
}

public sealed class EventRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<EventRepository> logger) : IEventRepository
{
    public Task<Result<Event>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rows = await connection.QueryAsync<EventRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        e.id AS Id,
                        e.venue_id AS VenueId,
                        e.name AS Name,
                        e.description AS Description,
                        e.start_time AS StartTime,
                        e.end_time AS EndTime,
                        e.ticket_price AS TicketPrice,
                        e.status AS Status
                    FROM
                        events AS e
                    WHERE
                        e.id = @Id
                    ",
                    new { Id = id },
                    cancellationToken: cancellationToken));

            return rows.ToList();
        })
        .Bind(rows => rows.Count == 0
            ? Failure<Event>(RepositoryErrors.NotFound)
            : Success(Event.Rehydrate(
                rows[0].Id,
                rows[0].VenueId,
                rows[0].Name,
                rows[0].Description,
                rows[0].StartTime,
                rows[0].EndTime,
                rows[0].TicketPrice,
                rows[0].Status)));

    public Task<Result<IReadOnlyList<Event>>> GetByVenueIdAsync(
        Guid venueId,
        CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async Task<IReadOnlyList<Event>> (connection) =>
        {
            var rows = await connection.QueryAsync<EventRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        e.id as Id,
                        e.venue_id as VenueId,
                        e.name as Name,
                        e.description as Description,
                        e.start_time as StartTime,
                        e.end_time as EndTime,
                        e.ticket_price as TicketPrice,
                        e.status as Status
                    FROM
                        events AS e
                    WHERE
                        e.venue_id = @VenueId
                    ORDER BY
                        e.start_time
                    ",
                    new { VenueId = venueId },
                    cancellationToken: cancellationToken));

            return rows.Select(r => Event.Rehydrate(
                r.Id,
                r.VenueId,
                r.Name,
                r.Description,
                r.StartTime,
                r.EndTime,
                r.TicketPrice,
                r.Status)).ToList();
        });

    public Task<Result<Event>> AddAsync(Event @event, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    INSERT INTO events
                    (
                        id,
                        venue_id,
                        name,
                        description,
                        start_time,
                        end_time,
                        ticket_price,
                        status
                    )
                    VALUES
                    (
                        @Id,
                        @VenueId,
                        @Name,
                        @Description,
                        @StartTime,
                        @EndTime,
                        @TicketPrice,
                        @Status
                    )
                    ",
                    new
                    {
                        @event.Id,
                        @event.VenueId,
                        @event.Name,
                        @event.Description,
                        @event.StartTime,
                        @event.EndTime,
                        @event.TicketPrice,
                        Status = @event.Status.ToString()
                    },
                    cancellationToken: cancellationToken));

            return @event;
        });

    public Task<Result<Event>> UpdateAsync(Event @event, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            return await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        events
                    SET
                        name = @Name,
                        description = @Description,
                        start_time = @StartTime,
                        end_time = @EndTime,
                        ticket_price = @TicketPrice,
                        status = @Status
                    WHERE
                        id = @Id
                    ",
                    new
                    {
                        @event.Id,
                        @event.Name,
                        @event.Description,
                        @event.StartTime,
                        @event.EndTime,
                        @event.TicketPrice,
                        Status = @event.Status.ToString()
                    },
                    cancellationToken: cancellationToken));
        })
        .Bind(rowsAffected => rowsAffected == 1
            ? Success(@event)
            : Failure<Event>(RepositoryErrors.NotFound));

    public Task<Result<bool>> TryPublishAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        events
                    SET
                        status = @NewStatus
                    WHERE
                        id = @Id AND
                        status = @RequiredStatus
                    ",
                new
                {
                    Id = eventId,
                    NewStatus = EventStatus.Published.ToString(),
                    RequiredStatus = EventStatus.Draft.ToString()
                },
                cancellationToken: cancellationToken));

            return rowsAffected == 1;
        });

    public Task<Result<bool>> TryCancelAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        events
                    SET
                        status = @NewStatus
                    WHERE
                        id = @Id AND
                        status = ANY(@RequiredStatus)
                    ",
                    new
                    {
                        Id = eventId,
                        NewStatus = EventStatus.Cancelled.ToString(),
                        RequiredStatus = new[]
                        {
                            EventStatus.Draft.ToString(),
                            EventStatus.Published.ToString()
                        }
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        });
}