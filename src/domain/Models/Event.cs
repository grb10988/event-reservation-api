using EventReservation.Domain.Infrastructure;

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
    public string Name { get; }
    public string Venue { get; }
    public string Description { get; }
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }
    public int Capacity { get; }
    public EventStatus Status { get; private set; }

    private Event(
        Guid id,
        string name,
        string venue,
        string description,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int capacity,
        EventStatus status)
    {
        Id = id;
        Name = name;
        Venue = venue;
        Description = description;
        StartTime = startTime;
        EndTime = endTime;
        Capacity = capacity;
        Status = status;
    }

    public static Result<Event> Create(
        string name,
        string venue,
        string description,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int capacity,
        TimeProvider timeProvider)
        => new Factory(name, venue, description, startTime, endTime, capacity, timeProvider).Create();

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

    private sealed class Factory : ResultConstructor<Event>
    {
        private readonly string _name;
        private readonly string _venue;
        private readonly string _description;
        private readonly DateTimeOffset _startTime;
        private readonly DateTimeOffset _endTime;
        private readonly int _capacity;
        private readonly TimeProvider _timeProvider;

        internal Factory(
            string name,
            string venue,
            string description,
            DateTimeOffset startTime,
            DateTimeOffset endTime,
            int capacity,
            TimeProvider timeProvider)
        {
            _name = name;
            _venue = venue;
            _description = description;
            _startTime = startTime;
            _endTime = endTime;
            _capacity = capacity;
            _timeProvider = timeProvider;
        }

        internal Result<Event> Create() => ExecuteSafely(() =>
        {
            Require(!string.IsNullOrWhiteSpace(_name), Errors.EmptyName);
            Require(!string.IsNullOrWhiteSpace(_venue), Errors.EmptyVenue);
            Require(!string.IsNullOrWhiteSpace(_description), Errors.EmptyDescription);
            Require(_startTime > _timeProvider.GetUtcNow(), Errors.InvalidStartTime);
            Require(_endTime > _startTime, Errors.InvalidEndTime);
            Require(_capacity > 0, Errors.InvalidCapacity);

            return HasErrors
                ? ToFailureResult()
                : Success(new Event(
                    Guid.CreateVersion7(),
                    _name,
                    _venue,
                    _description,
                    _startTime,
                    _endTime,
                    _capacity,
                    EventStatus.Draft));
        });
    }

    public static class Errors
    {
        private const string Context = "EVENT";
        public static ResultError EmptyName => new(Context, "Name is required.");
        public static ResultError EmptyVenue => new(Context, "Venue is required");
        public static ResultError EmptyDescription => new(Context, "Description is required.");
        public static ResultError InvalidStartTime => new(Context, "StartTime must be in the future.");
        public static ResultError InvalidEndTime => new(Context, "EndTime must be after StartTime.");
        public static ResultError InvalidCapacity => new(Context, "Capacity must be greater than zero.");
        public static ResultError CannotPublish => new(Context, "Only a Draft event can be published.");
        public static ResultError CannotCancel => new(Context, "A Cancelled event cannot be cancelled again.");
    }
}