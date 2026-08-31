using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Venues;

namespace EventReservation.Api.Endpoints;

public static class VenueEndpoints
{
    private sealed record UpdateVenueRequest(string Name, string Address, int Capacity);

    public static void MapVenueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/venues").WithTags("Venues");

        group.MapPost("/", async (CreateVenueCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<CreateVenueResult>(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/venues/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        });

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<GetVenueByIdResult>(new GetVenueByIdQuery(id), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapGet("/", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<IReadOnlyList<VenueSummary>>(new GetVenuesQuery(), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateVenueRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new UpdateVenueCommand(id, request.Name, request.Address, request.Capacity);
            var result = await dispatcher.SendAsync<UpdateVenueResult>(command, cancellationToken);

            return result.ToHttpResult();
        });
    }
}