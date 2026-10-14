using EventReservation.Domain.Construction;

namespace EventReservation.Domain.Models;

public enum EventSeatStatus { Available, Held, Reserved }

public sealed class EventSeat
{
    public Guid EventId { get; }
    public Guid SeatId { get; }
    public EventSeatStatus Status { get; private set; }
    public Guid? HeldBy { get; private set; }

    private EventSeat(Guid eventId, Guid seatId, EventSeatStatus status, Guid? heldBy)
    {
        EventId = eventId;
        SeatId = seatId;
        Status = status;
        HeldBy = heldBy;
    }

    public static Result<EventSeat> Create(Guid eventId, Guid seatId) =>
        new Factory(eventId, seatId).Create();

    internal static EventSeat Rehydrate(Guid eventId, Guid seatId, EventSeatStatus status, Guid? heldBy) =>
        new(eventId, seatId, status, heldBy);

    private static ResultErrors ValidateEventId(Guid eventId) =>
        ResultErrors.Collect(errors =>
        {
            errors.Validate(eventId, Errors.EmptyEventId);
        });

    private static ResultErrors ValidateSeatId(Guid seatId) =>
        ResultErrors.Collect(errors =>
        {
            errors.Validate(seatId, Errors.EmptySeatId);
        });

    public Result<EventSeat> Hold(Guid reservationId)
    {
        if (Status != EventSeatStatus.Available)
            return Failure<EventSeat>(Errors.CannotHold);

        Status = EventSeatStatus.Held;
        HeldBy = reservationId;
        return Success(this);
    }

    public Result<EventSeat> Reserve(Guid reservationId)
    {
        if (Status != EventSeatStatus.Held || HeldBy != reservationId)
            return Failure<EventSeat>(Errors.CannotReserve);

        Status = EventSeatStatus.Reserved;

        return Success(this);
    }

    public Result<EventSeat> Release(Guid reservationId)
    {
        if (Status is not (EventSeatStatus.Held or EventSeatStatus.Reserved) || HeldBy != reservationId)
            return Failure<EventSeat>(Errors.CannotRelease);

        Status = EventSeatStatus.Available;
        HeldBy = null;
        return Success(this);
    }

    private sealed class Factory : ModelFactory<EventSeat>
    {
        private readonly Guid _eventId;
        private readonly Guid _seatId;

        internal Factory(Guid eventId, Guid seatId)
        {
            _eventId = eventId;
            _seatId = seatId;
        }

        protected override Result<EventSeat> CreateInternal()
        {
            AddErrors(ValidateEventId(_eventId).Errors);
            AddErrors(ValidateSeatId(_seatId).Errors);

            return HasErrors
                ? ToFailureResult()
                : Success(new EventSeat(_eventId, _seatId, EventSeatStatus.Available, null));
        }
    }

    public static class Errors
    {
        private const string Context = "EVENT_SEAT";
        public static ResultError EmptyEventId => new(Context, "EventId is required.");
        public static ResultError EmptySeatId => new(Context, "SeatId is required.");
        public static ResultError CannotHold => new(Context, "Only an Available seat can be held.");
        public static ResultError CannotReserve => new(Context, "Only a seat held by this reservation can be reserved.");
        public static ResultError CannotRelease => new(Context, "Only a seat held or reserved by this reservation can be released.");
    }
}