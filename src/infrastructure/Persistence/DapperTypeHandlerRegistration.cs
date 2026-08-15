using Dapper;
using EventReservation.Domain.Models;

namespace EventReservation.Infrastructure.Persistence;

public static class DapperTypeHandlerRegistration
{
    public static void RegisterTypeHandlers()
    {
        SqlMapper.AddTypeHandler(new EnumTypeHandler<SeatStatus>());
        SqlMapper.AddTypeHandler(new EnumTypeHandler<EventStatus>());
        SqlMapper.AddTypeHandler(new EnumTypeHandler<ReservationStatus>());
        SqlMapper.AddTypeHandler(new EnumTypeHandler<OrderStatus>());
    }
}