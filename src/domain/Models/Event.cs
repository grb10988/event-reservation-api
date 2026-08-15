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
    public string Name { get; private set; }
    public string Description { get; private set; }
    public DateTimeOffset StartTime { get; private set; }
    public DateTimeOffset EndTime { get; private set; }
    public decimal TicketPrice { get; private set; }
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
        TimeProvider timeProvider) =>
        new Factory(
            venueId,
            name,
            description,
            startTime,
            endTime,
            ticketPrice,
            timeProvider).Create();

    internal static Event Rehydrate(
        Guid id,
        Guid venueId,
        string name,
        string description,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        decimal ticketPrice,
        EventStatus status) =>
        new(
            id,
            venueId,
            name,
            description,
            startTime,
            endTime,
            ticketPrice,
            status);

    private static ResultErrors ValidateVenueId(Guid venueId) => ResultErrors.Collect(errors =>
    {
        errors.Validate(venueId, Errors.EmptyVenueId);
    });

    private static ResultErrors ValidateName(string? name) => ResultErrors.Collect(errors =>
    {
        errors.Validate(name, Errors.EmptyName);
    });

    private static ResultErrors ValidateDescription(string? description) => ResultErrors.Collect(errors =>
    {
        errors.Validate(description, Errors.EmptyDescription);
    });

    private static ResultErrors ValidateStartTime(DateTimeOffset startTime, DateTimeOffset now) => ResultErrors.Collect(errors =>
    {
        errors.Validate(startTime > now, Errors.InvalidStartTime);
    });

    private static ResultErrors ValidateEndTime(DateTimeOffset endTime, DateTimeOffset startTime) => ResultErrors.Collect(errors =>
    {
        errors.Validate(endTime > startTime, Errors.InvalidEndTime);
    });

    private static ResultErrors ValidateReschedule(DateTimeOffset startTime, DateTimeOffset endTime, DateTimeOffset now) =>
        ResultErrors.Collect(errors =>
        {
            errors.AddError(ValidateStartTime(startTime, now).Errors);
            errors.AddError(ValidateEndTime(endTime, startTime).Errors);
        });

    private static ResultErrors ValidateTicketPrice(decimal ticketPrice) => ResultErrors.Collect(errors =>
    {
        errors.Validate(ticketPrice >= 0, Errors.InvalidTicketPrice);
    });

    public Result<Event> ChangeName(string newName) =>
        Success(this)
            .Ensure(_ => ValidateName(newName))
            .Tap(e => e.Name = newName);

    public Result<Event> ChangeDescription(string newDescription) =>
        Success(this)
            .Ensure(_ => ValidateDescription(newDescription))
            .Tap(e => e.Description = newDescription);

    public Result<Event> ChangeStartTime(DateTimeOffset newStartTime, TimeProvider timeProvider) =>
        Success(this)
            .Ensure(_ => ValidateStartTime(newStartTime, timeProvider.GetUtcNow()))
            .Ensure(_ => ValidateEndTime(EndTime, newStartTime))
            .Tap(e => e.StartTime = newStartTime);

    public Result<Event> ChangeEndTime(DateTimeOffset newEndTime) =>
        Success(this)
            .Ensure(_ => ValidateEndTime(newEndTime, StartTime))
            .Tap(e => e.EndTime = newEndTime);

    public Result<Event> Reschedule(DateTimeOffset newStartTime, DateTimeOffset newEndTime, TimeProvider timeProvider) =>
        Success(this)
            .Ensure(_ => ValidateReschedule(newStartTime, newEndTime, timeProvider.GetUtcNow()))
            .Tap(e =>
            {
                e.StartTime = newStartTime;
                e.EndTime = newEndTime;
            });

    public Result<Event> ChangeTicketPrice(decimal newTicketPrice) =>
        Success(this)
            .Ensure(_ => ValidateTicketPrice(newTicketPrice))
            .Tap(e => e.TicketPrice = newTicketPrice);

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
            var now = _timeProvider.GetUtcNow();

            AddErrors(ValidateVenueId(_venueId).Errors);
            AddErrors(ValidateName(_name).Errors);
            AddErrors(ValidateDescription(_description).Errors);
            AddErrors(ValidateStartTime(_startTime, now).Errors);
            AddErrors(ValidateEndTime(_endTime, _startTime).Errors);
            AddErrors(ValidateTicketPrice(_ticketPrice).Errors);

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