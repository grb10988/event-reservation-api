using EventReservation.Domain.Construction;

namespace EventReservation.Domain.Models;

public sealed class Venue
{
    public Guid Id { get; }
    public string Name { get; }
    public string Address { get; }
    public int Capacity { get; }

    private Venue(Guid id, string name, string address, int capacity)
    {
        Id = id;
        Name = name;
        Address = address;
        Capacity = capacity;
    }

    public static Result<Venue> Create(string name, string address, int capacity) =>
        new Factory(name, address, capacity).Create();

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
            Validate(_name, Errors.EmptyName);
            Validate(_address, Errors.EmptyAddress);
            Validate(_capacity > 0, Errors.InvalidCapacity);

            return HasErrors
                ? ToFailureResult()
                : Success(new Venue(Guid.CreateVersion7(), _name, _address, _capacity));
        }
    }

    public static class Errors
    {
        private const string Context = "VENUE";
        public static ResultError EmptyName => new(Context, "Name is required.");
        public static ResultError EmptyAddress => new(Context, "Address is required.");
        public static ResultError InvalidCapacity => new(Context, "Capacity must be greater than zero.");
    }
}