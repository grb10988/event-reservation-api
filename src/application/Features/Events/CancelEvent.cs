using EventReservation.Application.Abstractions;
using EventReservation.Application.Interfaces;

namespace EventReservation.Application.Features.Events;

public sealed record CancelEventResult(Guid Id);
public sealed record CancelEventCommand(Guid Id) : ICommand<CancelEventResult>;

public sealed class CancelEventCommandHandler(IEventRepository eventRepository)
    : ICommandHandler<CancelEventCommand, CancelEventResult>
{
    public Task<Result<CancelEventResult>> HandleAsync(
        CancelEventCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = eventRepository.GetByIdAsync(command.Id, cancellationToken)
            .Bind(evt => evt.Cancel())
            .Bind(evt => eventRepository.TryCancelAsync(evt.Id, cancellationToken)
                .Ensure(cancelled => cancelled, Errors.EventAlreadyCancelled)
                .Map(_ => new CancelEventResult(evt.Id)));

        return result;
    }

    public static class Errors
    {
        private const string Context = "CANCEL_EVENT";
        public static ResultError EventAlreadyCancelled => new(Context, "The requested event is already cancelled.");
    }
}
