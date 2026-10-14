namespace EventReservation.Application.Interfaces;

/// <summary>
/// Marker interface every repository interface extends, so infrastructure's
/// DI registration can discover repositories structurally instead of by a
/// name or namespace convention.
/// </summary>
public interface IRepository { }