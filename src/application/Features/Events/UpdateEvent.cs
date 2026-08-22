using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Events;

public sealed record UpdateEventResult(
    Guid Id,
    Guid VenueId,
    string Name,
    string Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    decimal TicketPrice,
    EventStatus Status);

public sealed record UpdateEventCommand(
    Guid Id,
    string Name,
    string Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    decimal TicketPrice) : ICommand<UpdateEventResult>;

public sealed class UpdateEventCommandHandler(IEventRepository eventRepository, TimeProvider timeProvider)
    : ICommandHandler<UpdateEventCommand, UpdateEventResult>
{
    public Task<Result<UpdateEventResult>> HandleAsync(
        UpdateEventCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = eventRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(evt => ValidateEvent(evt, command))
            .Bind(evt => eventRepository.UpdateAsync(evt, cancellationToken))
            .Map(evt => new UpdateEventResult(
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

    private Result<Event> ValidateEvent(Event @event, UpdateEventCommand command)
    {
        var validation = Combine(
            @event.ChangeName(command.Name),
            @event.ChangeDescription(command.Description),
            @event.Reschedule(command.StartTime, command.EndTime, timeProvider),
            @event.ChangeTicketPrice(command.TicketPrice));

        return validation.IsSuccess
            ? Success(@event)
            : Failure<Event>(validation.Errors);
    }
}