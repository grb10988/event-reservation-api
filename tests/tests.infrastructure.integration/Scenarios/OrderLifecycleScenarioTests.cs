using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Events;
using EventReservation.Application.Features.Orders;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EventReservation.Tests.Infrastructure.Integration.Scenarios;

[TestClass]
public class OrderLifecycleScenarioTests : ScenarioTestBase
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

    private async Task<(Guid OrderId, Guid ReservationId, Guid SeatId, Guid EventId)> SeedPendingOrderAsync()
    {
        var venue = Venue.Create("City Amphitheater", "123 Main St, Springfield", 5000).Value;
        Assert.IsNotNull(venue);
        var venueResult = await _venueRepository.AddAsync(venue);
        Assert.IsTrue(venueResult.IsSuccess, string.Join("; ", venueResult.ToFailureMessages()));

        var seat = Seat.Create(venue.Id, "A", 1, 1).Value;
        Assert.IsNotNull(seat);
        var seatResult = await _seatRepository.AddAsync(seat);
        Assert.IsTrue(seatResult.IsSuccess, string.Join("; ", seatResult.ToFailureMessages()));

        var evt = Event.Create(venue.Id, "Summer Concert", "An outdoor concert.", Clock.GetUtcNow().AddDays(30), Clock.GetUtcNow().AddDays(30).AddHours(3), 50.00m, Clock).Value;
        Assert.IsNotNull(evt);
        var eventResult = await _eventRepository.AddAsync(evt);
        Assert.IsTrue(eventResult.IsSuccess, string.Join("; ", eventResult.ToFailureMessages()));
        var publishResult = await _dispatcher.SendAsync<PublishEventResult>(new PublishEventCommand(evt.Id));
        Assert.IsTrue(publishResult.IsSuccess, string.Join("; ", publishResult.ToFailureMessages()));

        var customer = Customer.Create("Jane", "Doe", $"jane.{Guid.NewGuid()}@example.com").Value;
        Assert.IsNotNull(customer);
        var customerResult = await _customerRepository.AddAsync(customer);
        Assert.IsTrue(customerResult.IsSuccess, string.Join("; ", customerResult.ToFailureMessages()));

        var reservationResult = await _dispatcher.SendAsync<CreateReservationResult>(
            new CreateReservationCommand(seat.Id, evt.Id, customer.Id, 50.00m));
        Assert.IsTrue(reservationResult.IsSuccess, string.Join("; ", reservationResult.ToFailureMessages()));

        var orderResult = await _dispatcher.SendAsync<CreateOrderResult>(
            new CreateOrderCommand(customer.Id, [reservationResult.Value.Id]));
        Assert.IsTrue(orderResult.IsSuccess, string.Join("; ", orderResult.ToFailureMessages()));

        return (orderResult.Value.Id, reservationResult.Value.Id, seat.Id, evt.Id);
    }

    // ============================================================
    // Complete vs. Cancel
    // ============================================================

    [TestMethod]
    public async Task CompleteAndCancel_WhenRacedOnTheSamePendingOrder_ConvergesToOneConsistentOutcome()
    {
        // Arrange - both commands require the order to still be Pending, so
        // exactly one of them can ever claim it; the order must never end
        // up Completed with its reservation Cancelled, or vice versa
        var (orderId, reservationId, seatId, eventId) = await SeedPendingOrderAsync();

        // Act - fire both concurrently; Task.WhenAll here is only a sync
        // barrier, since CompleteOrderResult and CancelOrderResult are
        // different types and can't share a single typed result array
        var completeTask = _dispatcher.SendAsync<CompleteOrderResult>(new CompleteOrderCommand(orderId));
        var cancelTask = _dispatcher.SendAsync<CancelOrderResult>(new CancelOrderCommand(orderId));
        await Task.WhenAll(completeTask, cancelTask);
        var completeResult = await completeTask;
        var cancelResult = await cancelTask;

        // Assert - exactly one of the two actually won the order's own CAS
        Assert.AreNotEqual(completeResult.IsSuccess, cancelResult.IsSuccess);

        var order = await _orderRepository.GetByIdAsync(orderId);
        var reservation = await _reservationRepository.GetByIdAsync(reservationId);
        var seat = await _eventSeatRepository.GetByIdAsync(seatId, eventId);
        Assert.IsNotNull(order.Value);
        Assert.IsNotNull(reservation.Value);
        Assert.IsNotNull(seat.Value);

        if (completeResult.IsSuccess)
        {
            Assert.AreEqual(OrderStatus.Completed, order.Value.Status);
            Assert.AreEqual(ReservationStatus.Confirmed, reservation.Value.Status);
            Assert.AreEqual(EventSeatStatus.Reserved, seat.Value.Status);
        }
        else
        {
            Assert.AreEqual(OrderStatus.Cancelled, order.Value.Status);
            Assert.AreEqual(ReservationStatus.Cancelled, reservation.Value.Status);
            Assert.AreEqual(EventSeatStatus.Available, seat.Value.Status);
        }
    }
}