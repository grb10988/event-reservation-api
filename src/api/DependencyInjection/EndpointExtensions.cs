using EventReservation.Api.Endpoints;

namespace EventReservation.Api.DependencyInjection;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapVenueEndpoints();
        app.MapSeatEndpoints();
        app.MapEventEndpoints();
        app.MapReservationEndpoints();
        app.MapOrderEndpoints();
        app.MapCustomerEndpoints();

        return app;
    }
}