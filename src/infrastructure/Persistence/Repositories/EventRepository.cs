using Dapper;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed record EventRow(
    Guid Id,
    Guid VenueId,
    string Name,
    string Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    decimal TicketPrice,
    EventStatus Status);

public sealed class EventRepository(IDbConnectionFactory connectionFactory) : IEventRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public Task<Result<Event>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            return await connection.QuerySingleOrDefaultAsync<EventRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , venue_id
                        , name
                        , description
                        , start_time
                        , end_time
                        , ticket_price
                        , status
                    from events
                    where id = @Id",
                    new { Id = id },
                    cancellationToken: cancellationToken));
        }, ex => DatabaseExceptionMapper.Map(ex))
        .Bind(row => row is null
            ? Failure<Event>(RepositoryErrors.NotFound)
            : Success(Event.Rehydrate(
                row.Id,
                row.VenueId,
                row.Name,
                row.Description,
                row.StartTime,
                row.EndTime,
                row.TicketPrice,
                row.Status)));

    public Task<Result<IReadOnlyList<Event>>> GetByVenueIdAsync(Guid venueId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async Task<IReadOnlyList<Event>> () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<EventRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , venue_id
                        , name
                        , description
                        , start_time
                        , end_time
                        , ticket_price
                        , status
                    from events
                    where venue_id = @VenueId
                    order by start_time",
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
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<Event>> AddAsync(Event @event, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    insert into events (id, venue_id, name, description, start_time, end_time, ticket_price, status)
                    values (@Id, @VenueId, @Name, @Description, @StartTime, @EndTime, @TicketPrice, @Status)",
                    new
                    {
                        @event.Id,
                        @event.VenueId,
                        @event.Name,
                        @event.Description,
                        @event.StartTime,
                        @event.EndTime,
                        @event.TicketPrice,
                        @event.Status
                    },
                    cancellationToken: cancellationToken));

            return @event;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<Event>> UpdateAsync(Event @event, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            return await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update events
                    set
                        name = @Name
                        , description = @Description
                        , start_time = @StartTime
                        , end_time = @EndTime
                        , ticket_price = @TicketPrice
                        , status = @Status
                    where id = @Id",
                    new
                    {
                        @event.Id,
                        @event.Name,
                        @event.Description,
                        @event.StartTime,
                        @event.EndTime,
                        @event.TicketPrice,
                        @event.Status
                    },
                    cancellationToken: cancellationToken));
        }, ex => DatabaseExceptionMapper.Map(ex))
        .Bind(rowsAffected => rowsAffected == 1
            ? Success(@event)
            : Failure<Event>(RepositoryErrors.NotFound));

    public Task<Result<bool>> TryPublishAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update events
                    set status = @NewStatus
                    where id = @Id
                        and status = @RequiredSTatus",
                new
                {
                    Id = eventId,
                    NewStatus = EventStatus.Published,
                    RequiredStatus = EventStatus.Draft
                },
                cancellationToken: cancellationToken));

            return rowsAffected == 1;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<bool>> TryCancelAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update events
                    set status = @NewStatus
                    where id = @Id
                        and status in @RequiredStatuses
                    ",
                    new
                    {
                        Id = eventId,
                        NewStatus = EventStatus.Cancelled,
                        RequiredStatuses = new[] { EventStatus.Draft, EventStatus.Published }
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        }, ex => DatabaseExceptionMapper.Map(ex));
}