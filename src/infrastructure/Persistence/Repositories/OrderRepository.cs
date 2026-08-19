using System.Data;
using Dapper;
using EventReservation.Application.Interfaces.Repositories;
using EventReservation.Domain.Models;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed record OrderRow(Guid Id, Guid CustomerId, OrderStatus Status, string? ConfirmationNumber, DateTimeOffset CreatedAt);
internal sealed record OrderWithReservationRow(
    Guid Id,
    Guid CustomerId,
    OrderStatus Status,
    string? ConfirmationNumber,
    DateTimeOffset CreatedAt,
    Guid? ReservationId);

public sealed class OrderRepository(IDbConnectionFactory connectionFactory) : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    private static async Task<List<Guid>> LoadReservationIdsAsync(IDbConnection connection, Guid orderId, CancellationToken cancellationToken)
    {
        var reservationIds = await connection.QueryAsync<Guid>(
            new CommandDefinition(
                @"
                select reservation_id
                from order_reservations
                where order_id = @OrderId",
                new { OrderId = orderId },
                cancellationToken: cancellationToken));

        return reservationIds.ToList();
    }

    public Task<Result<Order>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var row = await connection.QuerySingleOrDefaultAsync<OrderRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , customer_id
                        , status
                        , confirmation_number
                        , created_at
                    from orders
                    where id = @Id",
                    new { Id = id },
                    cancellationToken: cancellationToken));
            if (row is null)
                return null;

            var reservationIds = await LoadReservationIdsAsync(connection, row.Id, cancellationToken);
            return Order.Rehydrate(
                row.Id,
                row.CustomerId,
                reservationIds,
                row.Status,
                row.ConfirmationNumber,
                row.CreatedAt);
        }, ex => DatabaseExceptionMapper.Map(ex))
        .Bind(order => order is null
            ? Failure<Order>(RepositoryErrors.NotFound)
            : Success(order));

    public Task<Result<Order>> GetByConfirmationNumberAsync(string confirmationNumber, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var row = await connection.QuerySingleOrDefaultAsync<OrderRow>(
                new CommandDefinition(
                    @"
                    select
                        id
                        , customer_id
                        , status
                        , confirmationNumber
                        , created_at
                    from orders
                    where confirmation_number = @ConfirmationNumber",
                    new { ConfirmationNumber = confirmationNumber },
                    cancellationToken: cancellationToken));

            if (row is null)
                return null;

            var reservationIds = await LoadReservationIdsAsync(connection, row.Id, cancellationToken);
            return Order.Rehydrate(
                row.Id,
                row.CustomerId,
                reservationIds,
                row.Status,
                row.ConfirmationNumber,
                row.CreatedAt);
        }, ex => DatabaseExceptionMapper.Map(ex))
        .Bind(order => order is null
            ? Failure<Order>(RepositoryErrors.NotFound)
            : Success(order));

    public Task<Result<IReadOnlyList<Order>>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async Task<IReadOnlyList<Order>> () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<OrderWithReservationRow>(
                new CommandDefinition(
                    @"
                    select
                        o.id
                        , o.customer_id
                        , o.status
                        , o.confirmation_number
                        , o.created_at
                        , orr.reservation_id
                    from orders as o
                    left join order_reservations as orr on orr.order_id = o.id
                    where o.customer_id = @CustomerId
                    order by o.created_at desc",
                    new { CustomerId = customerId },
                    cancellationToken: cancellationToken));

            return rows
                .GroupBy(r => new { r.Id, r.CustomerId, r.Status, r.ConfirmationNumber, r.CreatedAt })
                .Select(g => Order.Rehydrate(
                    g.Key.Id,
                    g.Key.CustomerId,
                    g.SelectMany(r =>
                        r.ReservationId is Guid reservationId
                            ? new[] { reservationId }
                            : Array.Empty<Guid>()).ToList(),
                    g.Key.Status,
                    g.Key.ConfirmationNumber,
                    g.Key.CreatedAt))
                .ToList();
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<Order>> AddAsync(Order order, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    insert into orders (id, customer_id, status, confirmation_number, created_at)
                    values (@Id, @CustomerId, @Status, @ConfirmationNumber, @CreatedAt)",
                    new
                    {
                        order.Id,
                        order.CustomerId,
                        order.Status,
                        order.ConfirmationNumber,
                        order.CreatedAt
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    insert into order_reservations (order_id, reservation_id)
                    select @OrderId, unnest(@ReservationIds::uuid[])",
                    new
                    {
                        OrderId = order.Id,
                        ReservationIds = order.ReservationIds.ToArray()
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            transaction.Commit();

            return order;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<bool>> TryCompleteAsync(Guid orderId, string confirmationNumber, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update orders
                    set status = @NewStatus
                        , confirmation_number = @ConfirmationNumber
                    where id = @Id
                        and status = @RequiredStatus",
                    new
                    {
                        Id = orderId,
                        NewStatus = OrderStatus.Completed,
                        ConfirmationNumber = confirmationNumber,
                        RequiredStatus = OrderStatus.Pending
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<bool>> TryCancelAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update orders
                    set status = @NewStatus
                    where id = @Id
                        and status = @RequiredStatus",
                    new
                    {
                        Id = orderId,
                        NewStatus = OrderStatus.Cancelled,
                        RequiredStatus = OrderStatus.Pending
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        }, ex => DatabaseExceptionMapper.Map(ex));

    public Task<Result<bool>> TryRefundAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Success().MapTry(async () =>
        {
            using var connection = _connectionFactory.CreateConnection();

            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    update orders
                    set status = @NewStatus
                    where id = @Id
                        and status = @RequiredStatus
                    ",
                    new
                    {
                        Id = orderId,
                        NewStatus = OrderStatus.Refunded,
                        RequireStatus = OrderStatus.Completed
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        }, ex => DatabaseExceptionMapper.Map(ex));
}