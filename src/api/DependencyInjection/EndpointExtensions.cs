using EventReservation.Api.Endpoints;

namespace EventReservation.Api.DependencyInjection;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        var apiGroup = app.MapGroup("/api");

        apiGroup.MapVenueEndpoints();
        apiGroup.MapSeatEndpoints();
        apiGroup.MapEventEndpoints();
        apiGroup.MapReservationEndpoints();
        apiGroup.MapOrderEndpoints();
        apiGroup.MapCustomerEndpoints();

        return app;
    }
}