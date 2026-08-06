using EventReservation.Domain.Infrastructure;

namespace EventReservation.Domain.Models;

public enum SeatStatus { Available, Held, Reserved }

public sealed class Seat
{
    public Guid Id { get; }
    public Guid EventId { get; }
    public string Section { get; }
    public int Row { get; }
    public int Number { get; }
    public SeatStatus Status { get; private set; }

    private Seat(Guid id, Guid eventId, string section, int row, int number, SeatStatus status)
    {
        Id = id;
        EventId = eventId;
        Section = section;
        Row = row;
        Number = number;
        Status = status;
    }

    public static Result<Seat> Create(Guid eventId, string section, int row, int number)
        => new Factory(eventId, section, row, number).Create();

    public sealed class Factory : ResultConstructor<Seat>
    {
        private readonly Guid _eventId;
        private readonly string _section;
        private readonly int _row;
        private readonly int _number;

        internal Factory(Guid eventId, string section, int row, int number)
        {
            _eventId = eventId;
            _section = section;
            _row = row;
            _number = number;
        }

        internal Result<Seat> Create() => ExecuteSafely(() =>
        {
            Require(_eventId != Guid.Empty, Errors.EmptyEventId);
            Require(!string.IsNullOrWhiteSpace(_section), Errors.EmptySection);
            Require(_row > 0, Errors.EmptyRow);
            Require(_number > 0, Errors.EmptyNumber);

            return HasErrors
                ? ToFailureResult()
                : Success(new Seat(Guid.CreateVersion7(), _eventId, _section, _row, _number, SeatStatus.Available));
        });
    }

    public static class Errors
    {
        private const string Context = "SEAT";
        public static ResultError EmptyEventId => new(Context, "EventId is required.");
        public static ResultError EmptySection => new(Context, "Section is required.");
        public static ResultError EmptyRow => new(Context, "Row is required.");
        public static ResultError EmptyNumber => new(Context, "Number is required.");
    }
}