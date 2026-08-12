using EventReservation.Domain.Construction;

namespace EventReservation.Domain.Models;

public enum OrderStatus
{
    Pending,
    Completed,
    Cancelled,
    Refunded
}

public sealed class Order
{
    public Guid Id { get; }
    public Guid CustomerId { get; }
    public IReadOnlyCollection<Guid> ReservationIds { get; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    private Order(
        Guid id,
        Guid customerId,
        IReadOnlyCollection<Guid> reservationIds,
        OrderStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        CustomerId = customerId;
        ReservationIds = reservationIds;
        Status = status;
        CreatedAt = createdAt;
    }

    public static Result<Order> Create(
        Guid customerId,
        IReadOnlyCollection<Guid> reservationIds,
        TimeProvider timeProvider) =>
        new Factory(customerId, reservationIds, timeProvider).Create();

    public Result<Order> Complete()
    {
        if (Status != OrderStatus.Pending)
            return Failure<Order>(Errors.CannotComplete);

        Status = OrderStatus.Completed;
        return Success(this);
    }

    public Result<Order> Cancel()
    {
        if (Status != OrderStatus.Pending)
            return Failure<Order>(Errors.CannotCancel);

        Status = OrderStatus.Cancelled;
        return Success(this);
    }

    public Result<Order> Refund()
    {
        if (Status != OrderStatus.Completed)
            return Failure<Order>(Errors.CannotRefund);

        Status = OrderStatus.Refunded;
        return Success(this);
    }

    private sealed class Factory : ModelFactory<Order>
    {
        private readonly Guid _customerId;
        private readonly IReadOnlyCollection<Guid> _reservationIds;
        private readonly TimeProvider _timeProvider;

        internal Factory(Guid customerId, IReadOnlyCollection<Guid> reservationIds, TimeProvider timeProvider)
        {
            _customerId = customerId;
            _reservationIds = reservationIds;
            _timeProvider = timeProvider;
        }

        protected override Result<Order> CreateInternal()
        {
            Validate(_customerId, Errors.EmptyCustomerId);
            Validate(_reservationIds is { Count: > 0 }, Errors.EmptyReservationIds);

            if (HasErrors)
                return ToFailureResult();

            Validate(_reservationIds.All(id => id != Guid.Empty), Errors.InvalidReservationId);
            Validate(_reservationIds.Distinct().Count() == _reservationIds.Count, Errors.DuplicateReservationIds);

            return HasErrors
                ? ToFailureResult()
                : Success(new Order(
                    Guid.CreateVersion7(),
                    _customerId,
                    _reservationIds,
                    OrderStatus.Pending,
                    _timeProvider.GetUtcNow()));
        }
    }

    public static class Errors
    {
        private const string Context = "ORDER";
        public static ResultError EmptyCustomerId => new(Context, "CustomerId is required.");
        public static ResultError EmptyReservationIds => new(Context, "At least one ReservationId is required.");
        public static ResultError InvalidReservationId => new(Context, "ReservationIds cannot contain an empty Guid.");
        public static ResultError DuplicateReservationIds => new(Context, "ReservationIds cannot contain duplicates.");
        public static ResultError CannotComplete => new(Context, "Only a Pending order can be completed.");
        public static ResultError CannotCancel => new(Context, "Only a Pending order can be cancelled.");
        public static ResultError CannotRefund => new(Context, "Only a Completed order can be refunded.");
    }
}