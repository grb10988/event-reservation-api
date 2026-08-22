using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;

namespace EventReservation.Application.Features.Venues;

public sealed record VenueSummary(Guid Id, string Name, string Address, int Capacity);
public sealed record GetVenuesQuery : IQuery<IReadOnlyList<VenueSummary>>;

public sealed class GetVenuesQueryHandler(IVenueRepository venueRepository)
    : IQueryHandler<GetVenuesQuery, IReadOnlyList<VenueSummary>>
{
    public Task<Result<IReadOnlyList<VenueSummary>>> HandleAsync(
        GetVenuesQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = venueRepository.GetAllAsync(cancellationToken)
            .Map(venues => (IReadOnlyList<VenueSummary>)venues
                .Select(v => new VenueSummary(v.Id, v.Name, v.Address, v.Capacity))
                .ToList());

        return result;
    }
}