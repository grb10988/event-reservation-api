using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Events;

public sealed record CreateEventResult(
    Guid Id,
    Guid VenueId,
    string Name,
    string Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    decimal TicketPrice,
    EventStatus Status);

public sealed record CreateEventCommand(
    Guid VenueId,
    string Name,
    string Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    decimal TicketPrice,
    EventStatus Status) : ICommand<CreateEventResult>;

public sealed class CreateEventCommandHandler(IEventRepository eventRepository, TimeProvider timeProvider)
    : ICommandHandler<CreateEventCommand, CreateEventResult>
{
    public Task<Result<CreateEventResult>> HandleAsync(
        CreateEventCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = Event.Create(
            command.VenueId,
            command.Name,
            command.Description,
            command.StartTime,
            command.EndTime,
            command.TicketPrice,
            timeProvider)
            .Bind(evt => eventRepository.AddAsync(evt, cancellationToken))
            .Map(evt => new CreateEventResult(
                evt.Id,
                evt.VenueId,
                evt.Name,
                evt.Description,
                evt.StartTime,
                evt.EndTime,
                evt.TicketPrice,
                evt.Status));

        return result;
    }
}