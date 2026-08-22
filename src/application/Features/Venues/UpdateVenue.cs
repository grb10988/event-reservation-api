using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;

namespace EventReservation.Application.Features.Venues;

public sealed record UpdateVenueResult(Guid Id, string Name, string Address, int Capacity);
public sealed record UpdateVenueCommand(Guid Id, string Name, string Address, int Capacity) : ICommand<UpdateVenueResult>;

public sealed class UpdateVenueCommandHandler(IVenueRepository venueRepository)
    : ICommandHandler<UpdateVenueCommand, UpdateVenueResult>
{
    public Task<Result<UpdateVenueResult>> HandleAsync(
        UpdateVenueCommand command,
        CancellationToken cancellationToken)
    {
        var result = venueRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(venue => ValidateVenue(venue, command))
            .Bind(venue => venueRepository.UpdateAsync(venue, cancellationToken))
            .Map(venue => new UpdateVenueResult(venue.Id, venue.Name, venue.Address, venue.Capacity));

        return result;
    }

    private static Result<Venue> ValidateVenue(Venue venue, UpdateVenueCommand command)
    {
        var validation = Combine(
            venue.ChangeName(command.Name),
            venue.ChangeAddress(command.Address),
            venue.ChangeCapacity(command.Capacity));

        return validation.IsSuccess
            ? Success(venue)
            : Failure<Venue>(validation.Errors);
    }
}