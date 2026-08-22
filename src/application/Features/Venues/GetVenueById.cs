using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;

namespace EventReservation.Application.Features.Venues;

public sealed record GetVenueByIdResult(Guid Id, string Name, string Address, int Capacity);
public sealed record GetVenueByIdQuery(Guid Id) : IQuery<GetVenueByIdResult>;

public sealed class GetVenueByIdQueryHandler(IVenueRepository venueRepository)
    : IQueryHandler<GetVenueByIdQuery, GetVenueByIdResult>
{
    public Task<Result<GetVenueByIdResult>> HandleAsync(
        GetVenueByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = venueRepository.GetByIdAsync(query.Id, cancellationToken)
            .Map(venue => new GetVenueByIdResult(venue.Id, venue.Name, venue.Address, venue.Capacity));

        return result;
    }
}