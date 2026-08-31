using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Events;

namespace EventReservation.Api.Endpoints;

public static class EventEndpoints
{
    public sealed record UpdateEventRequest(
        string Name,
        string Description,
        DateTimeOffset StartTime,
        DateTimeOffset EndTime,
        decimal TicketPrice);

    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/events").WithTags("Events");

        group.MapPost("/", async (CreateEventCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<CreateEventResult>(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/events/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        });

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<GetEventByIdResult>(new GetEventByIdQuery(id), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapGet("/", async (Guid venueId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<IReadOnlyList<EventSummary>>(new GetEventsByVenueQuery(venueId), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateEventRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateEventCommand(
                id,
                request.Name,
                request.Description,
                request.StartTime,
                request.EndTime,
                request.TicketPrice);

            var result = await dispatcher.SendAsync<UpdateEventResult>(command, cancellationToken);

            return result.ToHttpResult();
        });

        group.MapPost("/{id:guid}/publish", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<PublishEventResult>(new PublishEventCommand(id), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapPost("/{id:guid}/cancel", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<CancelEventResult>(new CancelEventCommand(id), cancellationToken);

            return result.ToHttpResult();
        });
    }
}