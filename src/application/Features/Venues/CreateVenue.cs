using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Venues;

public sealed record CreateVenueResult(Guid Id, string Name, string Address, int Capacity);
public sealed record CreateVenueCommand(string Name, string Address, int Capacity) : ICommand<CreateVenueResult>;

public sealed class CreateVenueCommandHandler(IVenueRepository venueRepository)
    : ICommandHandler<CreateVenueCommand, CreateVenueResult>
{
    public Task<Result<CreateVenueResult>> HandleAsync(
        CreateVenueCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = Venue.Create(command.Name, command.Address, command.Capacity)
            .Bind(venue => venueRepository.AddAsync(venue, cancellationToken))
            .Map(venue => new CreateVenueResult(venue.Id, venue.Name, venue.Address, venue.Capacity));

        return result;
    }
}