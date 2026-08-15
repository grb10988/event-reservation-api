using EventReservation.Domain.Construction;

namespace EventReservation.Domain.Models;

public sealed class Venue
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public string Address { get; private set; }
    public int Capacity { get; private set; }

    private const int NameLengthMax = 150;
    private const int AddressLengthMax = 300;

    private Venue(Guid id, string name, string address, int capacity)
    {
        Id = id;
        Name = name;
        Address = address;
        Capacity = capacity;
    }

    public static Result<Venue> Create(string name, string address, int capacity) =>
        new Factory(name, address, capacity).Create();

    internal static Venue Rehydrate(Guid id, string name, string address, int capacity) =>
        new(id, name, address, capacity);

    private static ResultErrors ValidateName(string? name) => ResultErrors.Collect(errors =>
    {
        if (errors.Validate(name, Errors.EmptyName))
            errors.Validate(name.Length <= NameLengthMax, Errors.NameOutOfRange);
    });

    private static ResultErrors ValidateAddress(string? address) => ResultErrors.Collect(errors =>
    {
        if (errors.Validate(address, Errors.EmptyAddress))
            errors.Validate(address.Length <= AddressLengthMax, Errors.AddressOutOfRange);
    });

    private static ResultErrors ValidateCapacity(int capacity) => ResultErrors.Collect(errors =>
    {
        errors.Validate(capacity > 0, Errors.InvalidCapacity);
    });

    public Result<Venue> ChangeName(string newName) =>
        Success(this)
            .Ensure(_ => ValidateName(newName))
            .Tap(v => v.Name = newName);

    public Result<Venue> ChangeAddress(string newAddress) =>
        Success(this)
            .Ensure(_ => ValidateAddress(newAddress))
            .Tap(v => v.Address = newAddress);

    public Result<Venue> ChangeCapacity(int newCapacity) =>
        Success(this)
            .Ensure(_ => ValidateCapacity(newCapacity))
            .Tap(v => v.Capacity = newCapacity);

    private sealed class Factory : ModelFactory<Venue>
    {
        private readonly string _name;
        private readonly string _address;
        private readonly int _capacity;

        internal Factory(string name, string address, int capacity)
        {
            _name = name;
            _address = address;
            _capacity = capacity;
        }

        protected override Result<Venue> CreateInternal()
        {
            AddErrors(ValidateName(_name).Errors);
            AddErrors(ValidateAddress(_address).Errors);
            AddErrors(ValidateCapacity(_capacity).Errors);

            return HasErrors
                ? ToFailureResult()
                : Success(new Venue(Guid.CreateVersion7(), _name, _address, _capacity));
        }
    }

    public static class Errors
    {
        private const string Context = "VENUE";
        public static ResultError EmptyName => new(Context, "Name is required.");
        public static ResultError NameOutOfRange => new(Context, $"Name cannot exceed {NameLengthMax} characters.");
        public static ResultError EmptyAddress => new(Context, "Address is required.");
        public static ResultError AddressOutOfRange => new(Context, $"Address cannot exceed {AddressLengthMax} characters.");
        public static ResultError InvalidCapacity => new(Context, "Capacity must be greater than zero.");
    }
}