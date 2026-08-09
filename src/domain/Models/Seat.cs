using EventReservation.Domain.Infrastructure;

namespace EventReservation.Domain.Models;

public enum SeatStatus { Available, Held, Reserved }

public sealed class Seat
{
    public Guid Id { get; }
    public Guid VenueId { get; }
    public string Section { get; }
    public int Row { get; }
    public int Number { get; }
    public SeatStatus Status { get; private set; }

    private Seat(
        Guid id,
        Guid venueId,
        string section,
        int row,
        int number,
        SeatStatus status)
    {
        Id = id;
        VenueId = venueId;
        Section = section;
        Row = row;
        Number = number;
        Status = status;
    }

    public static Result<Seat> Create(Guid venueId, string section, int row, int number)
        => new Factory(venueId, section, row, number).Create();

    public Result<Seat> Hold()
    {
        if (Status != SeatStatus.Available)
            return Failure<Seat>(Errors.CannotHold);

        Status = SeatStatus.Held;
        return Success(this);
    }

    public Result<Seat> Reserve()
    {
        if (Status != SeatStatus.Held)
            return Failure<Seat>(Errors.CannotReserve);

        Status = SeatStatus.Reserved;
        return Success(this);
    }

    public Result<Seat> Release()
    {
        if (Status is not (SeatStatus.Held or SeatStatus.Reserved))
            return Failure<Seat>(Errors.CannotRelease);

        Status = SeatStatus.Available;
        return Success(this);
    }

    public sealed class Factory : ResultConstructor<Seat>
    {
        private readonly Guid _venueId;
        private readonly string _section;
        private readonly int _row;
        private readonly int _number;

        internal Factory(Guid venueId, string section, int row, int number)
        {
            _venueId = venueId;
            _section = section;
            _row = row;
            _number = number;
        }

        internal Result<Seat> Create() => ExecuteSafely(() =>
        {
            Require(_venueId != Guid.Empty, Errors.EmptyVenueId);
            Require(!string.IsNullOrWhiteSpace(_section), Errors.EmptySection);
            Require(_row > 0, Errors.EmptyRow);
            Require(_number > 0, Errors.EmptyNumber);

            return HasErrors
                ? ToFailureResult()
                : Success(new Seat(
                    Guid.CreateVersion7(),
                    _venueId,
                    _section,
                    _row,
                    _number,
                    SeatStatus.Available));
        });
    }

    public static class Errors
    {
        private const string Context = "SEAT";
        public static ResultError EmptyVenueId => new(Context, "VenueId is required.");
        public static ResultError EmptySection => new(Context, "Section is required.");
        public static ResultError EmptyRow => new(Context, "Row is required.");
        public static ResultError EmptyNumber => new(Context, "Number is required.");
        public static ResultError CannotHold => new(Context, "Only an Available seat can be held.");
        public static ResultError CannotReserve => new(Context, "Only a Held seat can be reserved.");
        public static ResultError CannotRelease => new(Context, "Only a Held or Reserved seat can be released.");
    }
}