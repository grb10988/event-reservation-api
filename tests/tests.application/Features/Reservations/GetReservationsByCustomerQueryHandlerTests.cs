using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Reservations;

[TestClass]
public class GetReservationsByCustomerQueryHandlerTests
{
    private static readonly Guid SeatId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IReservationRepository _reservationRepository = null!;
    private GetReservationsByCustomerIdQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _reservationRepository = Substitute.For<IReservationRepository>();
        _handler = new GetReservationsByCustomerIdQueryHandler(_reservationRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenReservationsExist_ReturnsMappedSummaries()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(Now);
        var reservation = Reservation.Create(SeatId, EventId, CustomerId, 50.00m, timeProvider).Value;
        Assert.IsNotNull(reservation);
        _reservationRepository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(Success<IReadOnlyList<Reservation>>([reservation]));

        // Act
        var result = await _handler.HandleAsync(new GetReservationsByCustomerIdQuery(CustomerId));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.Value.Count);
        Assert.AreEqual(reservation.Id, result.Value[0].Id);
    }

    [TestMethod]
    public async Task HandleAsync_WhenNoReservationsExist_ReturnsEmptyList()
    {
        // Arrange
        _reservationRepository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(Success<IReadOnlyList<Reservation>>([]));

        // Act
        var result = await _handler.HandleAsync(new GetReservationsByCustomerIdQuery(CustomerId));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, result.Value.Count);
    }
}