using EventReservation.Application.Abstractions.Requests;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Events;

public sealed record PublishEventResult(Guid Id);
public sealed record PublishEventCommand(Guid Id) : ICommand<PublishEventResult>;

public sealed class PublishEventCommandHandler(
    IEventRepository eventRepository,
    IEventSeatRepository eventSeatRepository)
    : ICommandHandler<PublishEventCommand, PublishEventResult>
{
    public Task<Result<PublishEventResult>> HandleAsync(
        PublishEventCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = eventRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(evt => evt.Publish())
            .Bind(evt => ProvisionAndPublishAsync(evt, cancellationToken));

        return result;
    }

    private Task<Result<PublishEventResult>> ProvisionAndPublishAsync(Event evt, CancellationToken cancellationToken)
    {
        var result = eventSeatRepository.ProvisionAsync(evt.Id, evt.VenueId, cancellationToken)
            .Bind(() => eventRepository.TryPublishAsync(evt.Id, cancellationToken))
            .Ensure(published => published, Errors.EventNotDraft)
            .Map(_ => new PublishEventResult(evt.Id));

        return result;
    }

    public static class Errors
    {
        private const string Context = "PUBLISH_EVENT";

        public static ResultError EventNotDraft =>
            new(Context, "The requested event is not in Draft status and cannot be published.", ErrorCategory.Conflict);
    }
}