using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Reservations;

namespace EventReservation.Api.Endpoints;

public static class ReservationEndpoints
{
    public static void MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reservations").WithTags("Reservations");

        group.MapPost("/", async (CreateReservationCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<CreateReservationResult>(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/reservations/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        });

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<GetReservationByIdResult>(new GetReservationByIdQuery(id), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapGet("/", async (Guid customerId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<IReadOnlyList<ReservationSummary>>(new GetReservationsByCustomerIdQuery(customerId), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapPost("/{id:guid}/confirm", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<ConfirmReservationResult>(new ConfirmReservationCommand(id), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapPost("/{id:guid}/cancel", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<CancelReservationResult>(new CancelReservationCommand(id), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapPost("/{id:guid}/expire", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<ExpireReservationResult>(new ExpireReservationCommand(id), cancellationToken);

            return result.ToHttpResult();
        });
    }
}