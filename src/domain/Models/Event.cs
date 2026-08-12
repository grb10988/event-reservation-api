using EventReservation.Domain.Construction;

namespace EventReservation.Domain.Models;

public enum EventStatus
{
    Draft,
    Published,
    Cancelled
}

public sealed class Event
{
    public Guid Id { get; }
    public Guid VenueId { get; }
    public string Name { get; }
    public string Description { get; }
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }
    public decimal TicketPrice { get; }
    public EventStatus Status { get; private set; }

    private Event(
        Guid id,
        Guid venueId,
        string name,
        string description,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        decimal ticketPrice,
        EventStatus status)
    {
        Id = id;
        VenueId = venueId;
        Name = name;
        Description = description;
        StartTime = startTime;
        EndTime = endTime;
        TicketPrice = ticketPrice;
        Status = status;
    }

    public static Result<Event> Create(
        Guid venueId,
        string name,
        string description,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        decimal ticketPrice,
        TimeProvider timeProvider)
        => new Factory(
            venueId,
            name,
            description,
            startTime,
            endTime,
            ticketPrice,
            timeProvider).Create();

    public Result<Event> Publish()
    {
        if (Status != EventStatus.Draft)
            return Failure<Event>(Errors.CannotPublish);

        Status = EventStatus.Published;
        return Success(this);
    }

    public Result<Event> Cancel()
    {
        if (Status == EventStatus.Cancelled)
            return Failure<Event>(Errors.CannotCancel);

        Status = EventStatus.Cancelled;
        return Success(this);
    }

    private sealed class Factory : ModelFactory<Event>
    {
        private readonly Guid _venueId;
        private readonly string _name;
        private readonly string _description;
        private readonly DateTimeOffset _startTime;
        private readonly DateTimeOffset _endTime;
        private readonly decimal _ticketPrice;
        private readonly TimeProvider _timeProvider;

        internal Factory(
            Guid venueId,
            string name,
            string description,
            DateTimeOffset startTime,
            DateTimeOffset endTime,
            decimal ticketPrice,
            TimeProvider timeProvider)
        {
            _venueId = venueId;
            _name = name;
            _description = description;
            _startTime = startTime;
            _endTime = endTime;
            _ticketPrice = ticketPrice;
            _timeProvider = timeProvider;
        }

        protected override Result<Event> CreateInternal()
        {
            Validate(_venueId, Errors.EmptyVenueId);
            Validate(_name, Errors.EmptyName);
            Validate(_description, Errors.EmptyDescription);
            Validate(_startTime > _timeProvider.GetUtcNow(), Errors.InvalidStartTime);
            Validate(_endTime > _startTime, Errors.InvalidEndTime);
            Validate(_ticketPrice >= 0, Errors.InvalidTicketPrice);

            return HasErrors
                ? ToFailureResult()
                : Success(new Event(
                    Guid.CreateVersion7(),
                    _venueId,
                    _name,
                    _description,
                    _startTime,
                    _endTime,
                    _ticketPrice,
                    EventStatus.Draft));
        }
    }

    public static class Errors
    {
        private const string Context = "EVENT";
        public static ResultError EmptyVenueId => new(Context, "Venue is required");
        public static ResultError EmptyName => new(Context, "Name is required.");
        public static ResultError EmptyDescription => new(Context, "Description is required.");
        public static ResultError InvalidStartTime => new(Context, "StartTime must be in the future.");
        public static ResultError InvalidEndTime => new(Context, "EndTime must be after StartTime.");
        public static ResultError InvalidTicketPrice => new(Context, "TicketPrice cannot be negative.");
        public static ResultError CannotPublish => new(Context, "Only a Draft event can be published.");
        public static ResultError CannotCancel => new(Context, "A Cancelled event cannot be cancelled again.");
    }
}