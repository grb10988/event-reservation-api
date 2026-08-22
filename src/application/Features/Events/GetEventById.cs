using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Events;

public sealed record GetEventByIdResult(
    Guid Id,
    Guid VenueId,
    string Name,
    string Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    decimal TicketPrice,
    EventStatus Status);

public sealed record GetEventByIdQuery(Guid Id) : IQuery<GetEventByIdResult>;

public sealed class GetEventByIdQueryHandler(IEventRepository eventRepository)
    : IQueryHandler<GetEventByIdQuery, GetEventByIdResult>
{
    public Task<Result<GetEventByIdResult>> HandleAsync(
        GetEventByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = eventRepository.GetByIdAsync(query.Id, cancellationToken)
            .Map(evt => new GetEventByIdResult(
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