using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Events;
using EventReservation.Application.Features.Orders;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EventReservation.Tests.Infrastructure.Integration.Scenarios;

[TestClass]
public class CancelEventCascadeScenarioTests : ScenarioTestBase
{
    private IVenueRepository _venueRepository = null!;
    private ISeatRepository _seatRepository = null!;
    private IEventRepository _eventRepository = null!;
    private ICustomerRepository _customerRepository = null!;
    private IReservationRepository _reservationRepository = null!;
    private IEventSeatRepository _eventSeatRepository = null!;
    private IOrderRepository _orderRepository = null!;
    private IDispatcher _dispatcher = null!;

    [TestInitialize]
    public void Setup()
    {
        _venueRepository = Services.GetRequiredService<IVenueRepository>();
        _seatRepository = Services.GetRequiredService<ISeatRepository>();
        _eventRepository = Services.GetRequiredService<IEventRepository>();
        _customerRepository = Services.GetRequiredService<ICustomerRepository>();
        _reservationRepository = Services.GetRequiredService<IReservationRepository>();
        _eventSeatRepository = Services.GetRequiredService<IEventSeatRepository>();
        _orderRepository = Services.GetRequiredService<IOrderRepository>();
        _dispatcher = Services.GetRequiredService<IDispatcher>();
    }

    private async Task<Guid> SeedVenueAsync()
    {
        var venue = Venue.Create("City Amphitheater", "123 Main St, Springfield", 5000).Value;
        Assert.IsNotNull(venue);
        var result = await _venueRepository.AddAsync(venue);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    private async Task<Guid> SeedSeatAsync(Guid venueId, string section, int row, int number)
    {
        var seat = Seat.Create(venueId, section, row, number).Value;
        Assert.IsNotNull(seat);
        var result = await _seatRepository.AddAsync(seat);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    private async Task<Guid> SeedPublishedEventAsync(Guid venueId)
    {
        var evt = Event.Create(venueId, "Summer Concert", "An outdoor concert.", Clock.GetUtcNow().AddDays(30), Clock.GetUtcNow().AddDays(30).AddHours(3), 50.00m, Clock).Value;
        Assert.IsNotNull(evt);
        var addResult = await _eventRepository.AddAsync(evt);
        Assert.IsTrue(addResult.IsSuccess, string.Join("; ", addResult.ToFailureMessages()));
        var publishResult = await _dispatcher.SendAsync<PublishEventResult>(new PublishEventCommand(evt.Id));
        Assert.IsTrue(publishResult.IsSuccess, string.Join("; ", publishResult.ToFailureMessages()));
        return evt.Id;
    }

    private async Task<Guid> SeedCustomerAsync()
    {
        var customer = Customer.Create("Jane", "Doe", $"jane.{Guid.NewGuid()}@example.com").Value;
        Assert.IsNotNull(customer);
        var result = await _customerRepository.AddAsync(customer);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    private async Task<Guid> CreateHeldReservationAsync(Guid seatId, Guid eventId, Guid customerId)
    {
        var result = await _dispatcher.SendAsync<CreateReservationResult>(
            new CreateReservationCommand(seatId, eventId, customerId, 50.00m));
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    // ============================================================
    // A Completed order tied to a cancelled event is Refunded
    // ============================================================

    [TestMethod]
    public async Task CancelEvent_WhenAReservationBelongsToACompletedOrder_RefundsThatOrder()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId, "A", 1, 1);
        var eventId = await SeedPublishedEventAsync(venueId);
        var customerId = await SeedCustomerAsync();
        var reservationId = await CreateHeldReservationAsync(seatId, eventId, customerId);

        var orderResult = await _dispatcher.SendAsync<CreateOrderResult>(
            new CreateOrderCommand(customerId, [reservationId]));
        Assert.IsTrue(orderResult.IsSuccess, string.Join("; ", orderResult.ToFailureMessages()));
        var completeResult = await _dispatcher.SendAsync<CompleteOrderResult>(
            new CompleteOrderCommand(orderResult.Value.Id));
        Assert.IsTrue(completeResult.IsSuccess, string.Join("; ", completeResult.ToFailureMessages()));

        // Act
        var cancelEventResult = await _dispatcher.SendAsync<CancelEventResult>(new CancelEventCommand(eventId));

        // Assert - this is the exact regression this gaurds agains: before
        // the order_reservations join was fixed, this order was never found
        // and never refunded
        Assert.IsTrue(cancelEventResult.IsSuccess, string.Join("; ", cancelEventResult.ToFailureMessages()));

        var order = await _orderRepository.GetByIdAsync(orderResult.Value.Id);
        var reservation = await _reservationRepository.GetByIdAsync(reservationId);
        Assert.IsNotNull(order.Value);
        Assert.IsNotNull(reservation.Value);
        Assert.AreEqual(OrderStatus.Refunded, order.Value.Status);
        Assert.AreEqual(ReservationStatus.Cancelled, reservation.Value.Status);
    }

    // ============================================================
    // One order, several reservations, one event cancelled
    // ============================================================

    [TestMethod]
    public async Task CancelEvent_WhenOneOrderCoversSeveralReservationsForThatEvent_CancelsTheOrderOnce_AndReleasesEverySeat()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId1 = await SeedSeatAsync(venueId, "A", 1, 1);
        var seatId2 = await SeedSeatAsync(venueId, "A", 1, 2);
        var seatId3 = await SeedSeatAsync(venueId, "A", 1, 3);
        var eventId = await SeedPublishedEventAsync(venueId);
        var customerId = await SeedCustomerAsync();
        var reservationId1 = await CreateHeldReservationAsync(seatId1, eventId, customerId);
        var reservationId2 = await CreateHeldReservationAsync(seatId2, eventId, customerId);
        var reservationId3 = await CreateHeldReservationAsync(seatId3, eventId, customerId);

        var orderResult = await _dispatcher.SendAsync<CreateOrderResult>(
            new CreateOrderCommand(customerId, [reservationId1, reservationId2, reservationId3]));
        Assert.IsTrue(orderResult.IsSuccess, string.Join("; ", orderResult.ToFailureMessages()));

        // Act
        var cancelEventResult = await _dispatcher.SendAsync<CancelEventResult>(new CancelEventCommand(eventId));

        // Assert - the order's own CAS only ever admits one winner, so this
        // also proves CancelEventCommandHandler's dedup-by-order-id actually
        // prevented a second CancelOrderCommand dispatch for this order
        Assert.IsTrue(cancelEventResult.IsSuccess, string.Join("; ", cancelEventResult.ToFailureMessages()));

        var order = await _orderRepository.GetByIdAsync(orderResult.Value.Id);
        Assert.IsNotNull(order.Value);
        Assert.AreEqual(OrderStatus.Cancelled, order.Value.Status);

        foreach (var (reservationId, seatId) in new[] { (reservationId1, seatId1), (reservationId2, seatId2), (reservationId3, seatId3) })
        {
            var reservation = await _reservationRepository.GetByIdAsync(reservationId);
            var seat = await _eventSeatRepository.GetByIdAsync(seatId, eventId);
            Assert.IsNotNull(reservation.Value);
            Assert.IsNotNull(seat.Value);
            Assert.AreEqual(ReservationStatus.Cancelled, reservation.Value.Status);
            Assert.AreEqual(EventSeatStatus.Available, seat.Value.Status);
        }
    }
}