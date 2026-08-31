using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Orders;

namespace EventReservation.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        group.MapPost("/", async (CreateOrderCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<CreateOrderResult>(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/orders/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        });

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<GetOrderByIdResult>(new GetOrderByIdQuery(id), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapGet("/by-confirmation/{confirmationNumber}", async (
            string confirmationNumber,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<GetOrderByConfirmationNumberResult>(
                new GetOrderByConfirmationNumberQuery(confirmationNumber), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapGet("/", async (Guid customerId, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<IReadOnlyList<OrderSummary>>(new GetOrdersByCustomerIdQuery(customerId), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapPost("/{id:guid}/complete", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<CompleteOrderResult>(new CompleteOrderCommand(id), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapPost("/{id:guid}/cancel", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<CancelOrderResult>(new CancelOrderCommand(id), cancellationToken);

            return result.ToHttpResult();
        });
    }
}