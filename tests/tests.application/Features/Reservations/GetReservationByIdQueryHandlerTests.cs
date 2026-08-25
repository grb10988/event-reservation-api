using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Reservations;

[TestClass]
public class GetReservationByIdQueryHandlerTests
{
    private static readonly Guid SeatId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IReservationRepository _reservationRepository = null!;
    private GetReservationByIdQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _reservationRepository = Substitute.For<IReservationRepository>();
        _handler = new GetReservationByIdQueryHandler(_reservationRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenReservationExists_ReturnsMappedResult()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(Now);
        var reservation = Reservation.Create(SeatId, EventId, CustomerId, 50.00m, timeProvider).Value;
        Assert.IsNotNull(reservation);
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));

        // Act
        var result = await _handler.HandleAsync(new GetReservationByIdQuery(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(reservation.Id, result.Value.Id);
        Assert.AreEqual(reservation.SeatId, result.Value.SeatId);
        Assert.AreEqual(reservation.Status, result.Value.Status);
    }

    [TestMethod]
    public async Task HandleAsync_WhenReservationDoesNotExist_PropagatesFailure()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var notFoundError = new ResultError("TEST", "not found");
        _reservationRepository.GetByIdAsync(reservationId, Arg.Any<CancellationToken>()).Returns(Failure<Reservation>(notFoundError));

        // Act
        var result = await _handler.HandleAsync(new GetReservationByIdQuery(reservationId));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), notFoundError);
    }
}