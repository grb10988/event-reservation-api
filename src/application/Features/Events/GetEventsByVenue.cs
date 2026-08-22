using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Events;

public sealed record EventSummary(
    Guid Id,
    Guid VenueId,
    string Name,
    string Description,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    decimal TicketPrice,
    EventStatus Status);

public sealed record GetEventsByVenueQuery(Guid VenueId) : IQuery<IReadOnlyList<EventSummary>>;

public sealed class GetEventsByVenueQueryHandler(IEventRepository eventRepository)
    : IQueryHandler<GetEventsByVenueQuery, IReadOnlyList<EventSummary>>
{
    public Task<Result<IReadOnlyList<EventSummary>>> HandleAsync(
        GetEventsByVenueQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = eventRepository.GetByVenueIdAsync(query.VenueId, cancellationToken)
            .Map(events => (IReadOnlyList<EventSummary>)events
                .Select(e => new EventSummary(
                    e.Id,
                    e.VenueId,
                    e.Name,
                    e.Description,
                    e.StartTime,
                    e.EndTime,
                    e.TicketPrice,
                    e.Status))
                .ToList());

        return result;
    }
}