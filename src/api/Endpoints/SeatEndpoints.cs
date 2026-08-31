using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Seats;

namespace EventReservation.Api.Endpoints;

public static class SeatEndpoints
{
    public static void MapSeatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/seats").WithTags("Seats");

        group.MapPost("/{id:guid}/hold", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<HoldSeatResult>(new HoldSeatCommand(id), cancellationToken);
            
            return result.ToHttpResult();
        });

        group.MapPost("/{id:guid}/reserve", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<ReserveSeatResult>(new ReserveSeatCommand(id), cancellationToken);
            
            return result.ToHttpResult();
        });

        group.MapPost("/{id:guid}/release", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<ReleaseSeatResult>(new ReleaseSeatCommand(id), cancellationToken);
            
            return result.ToHttpResult();
        });
    }
}