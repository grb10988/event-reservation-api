using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Customers;

namespace EventReservation.Api.Endpoints;

public static class CustomerEndpoints
{
    private sealed record UpdateCustomerRequest(string FirstName, string LastName, string Email);

    public static void MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/customers").WithTags("Customers");

        group.MapPost("/", async (CreateCustomerCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<CreateCustomerResult>(command, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/customers/{result.Value.Id}", result.Value)
                : result.ToHttpResult();
        });

        group.MapGet("/{id:guid}", async (Guid id, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<GetCustomerByIdResult>(new GetCustomerByIdQuery(id), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapGet("/by-email", async (string email, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync<GetCustomerByEmailResult>(new GetCustomerByEmailQuery(email), cancellationToken);

            return result.ToHttpResult();
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCustomerRequest request,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateCustomerCommand(id, request.FirstName, request.LastName, request.Email);
            var result = await dispatcher.SendAsync<UpdateCustomerResult>(command, cancellationToken);

            return result.ToHttpResult();
        });
    }
}