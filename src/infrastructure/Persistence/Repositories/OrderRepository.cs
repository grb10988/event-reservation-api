using System.Data;
using Dapper;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EventReservation.Infrastructure.Persistence.Repositories;

internal sealed class OrderRow
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public OrderStatus Status { get; init; }
    public string? ConfirmationNumber { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

internal sealed class OrderWithReservationRow
{
    public Guid Id { get; init; }
    public Guid CustomerId { get; init; }
    public OrderStatus Status { get; init; }
    public string? ConfirmationNumber { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public Guid? ReservationId { get; init; }
}

public sealed class OrderRepository(
    IDbConnectionFactory connectionFactory,
    ILogger<OrderRepository> logger) : IOrderRepository
{
    private static async Task<List<Guid>> LoadReservationIdsAsync(
        IDbConnection connection,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var reservationIds = await connection.QueryAsync<Guid>(
            new CommandDefinition(
                @"
                SELECT
                    orr.reservation_id AS ReservationId
                FROM
                    order_reservations AS orr
                WHERE
                    orr.order_id = @OrderId
                ",
                new { OrderId = orderId },
                cancellationToken: cancellationToken));

        return reservationIds.ToList();
    }

    public Task<Result<Order>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rows = (await connection.QueryAsync<OrderRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        o.id AS Id,
                        o.customer_id AS CustomerId,
                        o.status AS Status,
                        o.confirmation_number AS ConfirmationNumber,
                        o.created_at AS CreatedAt
                    FROM
                        orders AS o
                    WHERE
                        o.id = @Id
                    ",
                    new { Id = id },
                    cancellationToken: cancellationToken))).ToList();

            if (rows.Count == 0)
                return (Rows: rows, ReservationIds: new List<Guid>());

            var reservationIds = await LoadReservationIdsAsync(connection, rows[0].Id, cancellationToken);

            return (Rows: rows, ReservationIds: reservationIds);

        })
        .Bind(result => result.Rows.Count == 0
            ? Failure<Order>(RepositoryErrors.NotFound)
            : Success(Order.Rehydrate(
                result.Rows[0].Id,
                result.Rows[0].CustomerId,
                result.ReservationIds,
                result.Rows[0].Status,
                result.Rows[0].ConfirmationNumber,
                result.Rows[0].CreatedAt)));

    public Task<Result<Order>> GetByConfirmationNumberAsync(string confirmationNumber, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rows = (await connection.QueryAsync<OrderRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        o.id AS Id,
                        o.customer_id AS CustomerId,
                        o.status AS Status,
                        o.confirmation_number AS ConfirmationNumber,
                        o.created_at AS CreatedAt
                    FROM
                        orders AS o
                    WHERE
                        o.confirmation_number = @ConfirmationNumber
                    ",
                    new { ConfirmationNumber = confirmationNumber },
                    cancellationToken: cancellationToken))).ToList();

            if (rows.Count == 0)
                return (Rows: rows, ReservationIds: new List<Guid>());

            var reservationIds = await LoadReservationIdsAsync(connection, rows[0].Id, cancellationToken);

            return (Rows: rows, ReservationIds: reservationIds);

        })
        .Bind(result => result.Rows.Count == 0
            ? Failure<Order>(RepositoryErrors.NotFound)
            : Success(Order.Rehydrate(
                result.Rows[0].Id,
                result.Rows[0].CustomerId,
                result.ReservationIds,
                result.Rows[0].Status,
                result.Rows[0].ConfirmationNumber,
                result.Rows[0].CreatedAt)));

    public Task<Result<IReadOnlyList<Order>>> GetByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async Task<IReadOnlyList<Order>> (connection) =>
        {
            var rows = await connection.QueryAsync<OrderWithReservationRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        o.id AS Id,
                        o.customer_id AS CustomerId,
                        o.status AS Status,
                        o.confirmation_number AS ConfirmationNumber,
                        o.created_at AS CreatedAt,
                        orr.reservation_id AS ReservationId
                    FROM
                        orders AS o
                    LEFT JOIN
                        order_reservations AS orr
                            ON orr.order_id = o.id
                    WHERE
                        o.customer_id = @CustomerId
                    ORDER BY
                        o.created_at DESC
                    ",
                    new { CustomerId = customerId },
                    cancellationToken: cancellationToken));

            return rows
                .GroupBy(r => new
                {
                    r.Id,
                    r.CustomerId,
                    r.Status,
                    r.ConfirmationNumber,
                    r.CreatedAt
                })
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
        });

    public Task<Result<Order>> GetByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rows = (await connection.QueryAsync<OrderRow>(
                new CommandDefinition(
                    @"
                    SELECT
                        o.id AS Id,
                        o.customer_id AS CustomerId,
                        o.status AS Status,
                        o.confirmation_number AS ConfirmationNumber,
                        o.created_at AS CreatedAt
                    FROM
                        orders AS o
                    JOIN
                        order_reservations AS orr ON orr.order_id = o.id
                    WHERE
                        orr.reservation_id = @ReservationId
                    ",
                    new { ReservationId = reservationId },
                    cancellationToken: cancellationToken))).ToList();

            if (rows.Count == 0)
                return (Rows: rows, ReservationIds: new List<Guid>());

            var reservationIds = await LoadReservationIdsAsync(connection, rows[0].Id, cancellationToken);

            return (Rows: rows, ReservationIds: reservationIds);
        })
        .Bind(result => result.Rows.Count == 0
            ? Failure<Order>(RepositoryErrors.NotFound)
            : Success(Order.Rehydrate(
                result.Rows[0].Id,
                result.Rows[0].CustomerId,
                result.ReservationIds,
                result.Rows[0].Status,
                result.Rows[0].ConfirmationNumber,
                result.Rows[0].CreatedAt)));

    public Task<Result<Order>> AddAsync(Order order, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            connection.Open();
            using var transaction = connection.BeginTransaction();

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    INSERT INTO orders
                    (
                        id,
                        customer_id,
                        status,
                        confirmation_number,
                        created_at
                    )
                    VALUES
                    (
                        @Id,
                        @CustomerId,
                        @Status,
                        @ConfirmationNumber,
                        @CreatedAt
                    )
                    ",
                    new
                    {
                        order.Id,
                        order.CustomerId,
                        Status = order.Status.ToString(),
                        order.ConfirmationNumber,
                        order.CreatedAt
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            var reservationIds = order.ReservationIds.ToArray();

            // Re-verifies every reservation is still Held and unexpired at the
            // instant of the real insert, inside this same transaction
            var linkedCount = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    INSERT INTO order_reservations
                    (
                        order_id,
                        reservation_id
                    )
                    SELECT
                        @OrderId,
                        r.id
                    FROM
                        reservations AS r
                    WHERE
                        r.id = ANY(@ReservationIds::uuid[]) AND
                        r.status = @RequiredStatus AND
                        r.hold_expires_at > now()
                    ",
                    new
                    {
                        OrderId = order.Id,
                        ReservationIds = reservationIds,
                        RequiredStatus = ReservationStatus.Held.ToString()
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (linkedCount == reservationIds.Length)
            {
                transaction.Commit();
                return linkedCount;
            }

            transaction.Rollback();
            return 0;
        })
        .Ensure(linkedCount => linkedCount == order.ReservationIds.Count, RepositoryErrors.ReservationNoLongerAvailable)
        .Map(_ => order);

    public Task<Result<bool>> TryCompleteAsync(
        Guid orderId,
        string confirmationNumber,
        CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        orders AS o
                    SET
                        status = @NewStatus,
                        confirmation_number = @ConfirmationNumber
                    WHERE
                        o.id = @Id AND
                        o.status = @RequiredStatus AND
                        NOT EXISTS (
                            SELECT 1
                            FROM order_reservations AS orr
                            JOIN reservations AS r ON r.id = orr.reservation_id
                            WHERE
                                orr.order_id = o.id AND
                                r.status <> @ConfirmedStatus
                        )
                    ",
                    new
                    {
                        Id = orderId,
                        NewStatus = OrderStatus.Completed.ToString(),
                        ConfirmationNumber = confirmationNumber,
                        RequiredStatus = OrderStatus.Pending.ToString(),
                        ConfirmedStatus = ReservationStatus.Confirmed.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        });

    public Task<Result<bool>> TryCancelAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        orders
                    SET
                        status = @NewStatus
                    WHERE
                        id = @Id AND
                        status = @RequiredStatus
                    ",
                    new
                    {
                        Id = orderId,
                        NewStatus = OrderStatus.Cancelled.ToString(),
                        RequiredStatus = OrderStatus.Pending.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        });

    public Task<Result<bool>> TryRefundAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        RepositoryOperations.ExecuteAsync(connectionFactory, logger, async connection =>
        {
            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"
                    UPDATE
                        orders
                    SET
                        status = @NewStatus
                    WHERE
                        id = @Id AND
                        status = @RequiredStatus
                    ",
                    new
                    {
                        Id = orderId,
                        NewStatus = OrderStatus.Refunded.ToString(),
                        RequiredStatus = OrderStatus.Completed.ToString()
                    },
                    cancellationToken: cancellationToken));

            return rowsAffected == 1;
        });
}