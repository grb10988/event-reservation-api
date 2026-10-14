using EventReservation.Domain.Construction;

namespace EventReservation.Domain.Models;

public sealed class Seat
{
    public Guid Id { get; }
    public Guid VenueId { get; }
    public string Section { get; }
    public int Row { get; }
    public int Number { get; }

    private Seat(
        Guid id,
        Guid venueId,
        string section,
        int row,
        int number)
    {
        Id = id;
        VenueId = venueId;
        Section = section;
        Row = row;
        Number = number;
    }

    public static Result<Seat> Create(Guid venueId, string section, int row, int number)
        => new Factory(venueId, section, row, number).Create();

    internal static Seat Rehydrate(Guid id, Guid venueId, string section, int row, int number) =>
        new(id, venueId, section, row, number);

    private static ResultErrors ValidateVenueId(Guid venueId) => ResultErrors.Collect(errors =>
    {
        errors.Validate(venueId, Errors.EmptyVenueId);
    });

    private static ResultErrors ValidateSection(string? section) => ResultErrors.Collect(errors =>
    {
        errors.Validate(section, Errors.EmptySection);
    });

    private static ResultErrors ValidateRow(int row) => ResultErrors.Collect(errors =>
    {
        errors.Validate(row > 0, Errors.EmptyRow);
    });

    private static ResultErrors ValidateNumber(int number) => ResultErrors.Collect(errors =>
    {
        errors.Validate(number > 0, Errors.EmptyNumber);
    });

    public sealed class Factory : ModelFactory<Seat>
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

        protected override Result<Seat> CreateInternal()
        {
            AddErrors(ValidateVenueId(_venueId).Errors);
            AddErrors(ValidateSection(_section).Errors);
            AddErrors(ValidateRow(_row).Errors);
            AddErrors(ValidateNumber(_number).Errors);

            return HasErrors
                ? ToFailureResult()
                : Success(new Seat(
                    Guid.CreateVersion7(),
                    _venueId,
                    _section,
                    _row,
                    _number));
        }
    }

    public static class Errors
    {
        private const string Context = "SEAT";
        public static ResultError EmptyVenueId => new(Context, "VenueId is required.");
        public static ResultError EmptySection => new(Context, "Section is required.");
        public static ResultError EmptyRow => new(Context, "Row is required.");
        public static ResultError EmptyNumber => new(Context, "Number is required.");
    }
}