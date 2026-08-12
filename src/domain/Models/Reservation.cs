using EventReservation.Domain.Construction;

namespace EventReservation.Domain.Models;

public enum ReservationStatus
{
    Held,
    Confirmed,
    Cancelled,
    Expired
}

public sealed class Reservation
{
    public static readonly TimeSpan DefaultHoldDuration = TimeSpan.FromMinutes(15);

    public Guid Id { get; }
    public Guid SeatId { get; }
    public Guid EventId { get; }
    public Guid CustomerId { get; }
    public decimal Price { get; }
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset HoldExpiresAt { get; }

    private Reservation(
        Guid id,
        Guid seatId,
        Guid eventId,
        Guid customerId,
        decimal price,
        ReservationStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset holdExpiresAt)
    {
        Id = id;
        SeatId = seatId;
        EventId = eventId;
        CustomerId = customerId;
        Price = price;
        Status = status;
        CreatedAt = createdAt;
        HoldExpiresAt = holdExpiresAt;
    }

    public static Result<Reservation> Create(
        Guid seatId,
        Guid eventId,
        Guid customerId,
        decimal price,
        TimeProvider timeProvider,
        TimeSpan? holdDuration = null) =>
        new Factory(seatId, eventId, customerId, price, timeProvider, holdDuration).Create();

    public Result<Reservation> Confirm()
    {
        if (Status != ReservationStatus.Held)
            return Failure<Reservation>(Errors.CannotConfirm);

        Status = ReservationStatus.Confirmed;
        return Success(this);
    }

    public Result<Reservation> Cancel()
    {
        if (Status is not (ReservationStatus.Held or ReservationStatus.Confirmed))
            return Failure<Reservation>(Errors.CannotCancel);

        Status = ReservationStatus.Cancelled;
        return Success(this);
    }

    public Result<Reservation> Expire()
    {
        if (Status != ReservationStatus.Held)
            return Failure<Reservation>(Errors.CannotExpire);

        Status = ReservationStatus.Expired;
        return Success(this);
    }

    private sealed class Factory : ModelFactory<Reservation>
    {
        private readonly Guid _seatId;
        private readonly Guid _eventId;
        private readonly Guid _customerId;
        private readonly decimal _price;
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan? _holdDuration;

        internal Factory(
            Guid seatId,
            Guid eventId,
            Guid customerId,
            decimal price,
            TimeProvider timeProvider,
            TimeSpan? holdDuration)
        {
            _seatId = seatId;
            _eventId = eventId;
            _customerId = customerId;
            _price = price;
            _timeProvider = timeProvider;
            _holdDuration = holdDuration;
        }

        protected override Result<Reservation> CreateInternal()
        {
            Validate(_seatId, Errors.EmptySeatId);
            Validate(_eventId, Errors.EmptyEventId);
            Validate(_customerId, Errors.EmptyCustomerId);
            Validate(_price >= 0, Errors.InvalidPrice);
            Validate(_holdDuration is null or { Ticks: > 0 }, Errors.InvalidHoldDuration);

            if (HasErrors)
                return ToFailureResult();

            var now = _timeProvider.GetUtcNow();
            var duration = _holdDuration ?? DefaultHoldDuration;

            return Success(new Reservation(
                Guid.CreateVersion7(),
                _seatId,
                _eventId,
                _customerId,
                _price,
                ReservationStatus.Held,
                now,
                now + duration));
        }
    }

    public static class Errors
    {
        private const string Context = "RESERVATION";
        public static ResultError EmptySeatId => new(Context, "SeatId is required.");
        public static ResultError EmptyEventId => new(Context, "EventId is required.");
        public static ResultError EmptyCustomerId => new(Context, "CustomerId is required.");
        public static ResultError InvalidHoldDuration => new(Context, "HoldDuration must be greater than zero when provided.");
        public static ResultError InvalidPrice => new(Context, "Price cannot be negative.");
        public static ResultError CannotConfirm => new(Context, "Only a Held reservation can be confirmed.");
        public static ResultError CannotCancel => new(Context, "Only a Held or Confirmed reservation can be cancelled.");
        public static ResultError CannotExpire => new(Context, "Only a Held reservation can expire.");
    }
}