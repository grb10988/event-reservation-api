using System.Net.Mail;
using EventReservation.Domain.Construction;

namespace EventReservation.Domain.Models;

public sealed class Customer
{
    public Guid Id { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public string Email { get; }

    private Customer(Guid id, string firstName, string lastName, string email)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
    }

    public static Result<Customer> Create(string firstName, string lastName, string email) =>
        new Factory(firstName, lastName, email).Create();

    private sealed class Factory : ModelFactory<Customer>
    {
        private readonly string _firstName;
        private readonly string _lastName;
        private readonly string _email;

        internal Factory(string firstName, string lastName, string email)
        {
            _firstName = firstName;
            _lastName = lastName;
            _email = email;
        }

        protected override Result<Customer> CreateInternal()
        {
            Validate(_firstName, Errors.EmptyFirstName);
            Validate(_lastName, Errors.EmptyLastName);
            Validate(_email, Errors.EmptyEmail);

            if (HasErrors)
                return ToFailureResult();

            Validate(MailAddress.TryCreate(_email, out _), Errors.InvalidEmail);

            return HasErrors
                ? ToFailureResult()
                : Success(new Customer(Guid.CreateVersion7(), _firstName, _lastName, _email));
        }
    }

    public static class Errors
    {
        private const string Context = "CUSTOMER";
        public static ResultError EmptyFirstName => new(Context, "FirstName is required.");
        public static ResultError EmptyLastName => new(Context, "LastName is required.");
        public static ResultError EmptyEmail => new(Context, "Email is required.");
        public static ResultError InvalidEmail => new(Context, "Email is not a valid email address.");
    }
}