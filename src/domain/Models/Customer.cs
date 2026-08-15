using System.Net.Mail;
using EventReservation.Domain.Construction;

namespace EventReservation.Domain.Models;

public sealed class Customer
{
    public Guid Id { get; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; private set; }

    private Customer(Guid id, string firstName, string lastName, string email)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
    }

    public static Result<Customer> Create(string firstName, string lastName, string email) =>
        new Factory(firstName, lastName, email).Create();

    internal static Customer Rehydrate(Guid id, string firstName, string lastName, string email) =>
        new(id, firstName, lastName, email);

    private static ResultErrors ValidateFirstName(string? firstName) => ResultErrors.Collect(errors =>
    {
        errors.Validate(firstName, Errors.EmptyFirstName);
    });

    private static ResultErrors ValidateLastName(string? lastName) => ResultErrors.Collect(errors =>
    {
        errors.Validate(lastName, Errors.EmptyLastName);
    });

    private static ResultErrors ValidateEmail(string? email) => ResultErrors.Collect(errors =>
    {
        if (errors.Validate(email, Errors.EmptyEmail))
            errors.Validate(MailAddress.TryCreate(email, out _), Errors.InvalidEmail);
    });

    public Result<Customer> ChangeFirstName(string newFirstName) =>
        Success(this)
            .Ensure(_ => ValidateFirstName(newFirstName))
            .Tap(c => c.FirstName = newFirstName);

    public Result<Customer> ChangeLastName(string newLastName) =>
        Success(this)
            .Ensure(_ => ValidateLastName(newLastName))
            .Tap(c => c.LastName = newLastName);

    public Result<Customer> ChangeEmail(string newEmail) =>
        Success(this)
            .Ensure(_ => ValidateEmail(newEmail))
            .Tap(c => c.Email = newEmail);
    
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
            AddErrors(ValidateFirstName(_firstName).Errors);
            AddErrors(ValidateLastName(_lastName).Errors);
            AddErrors(ValidateEmail(_email).Errors);

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