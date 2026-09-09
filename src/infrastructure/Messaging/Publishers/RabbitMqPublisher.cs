using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EventReservation.Application.Abstractions.IntegrationEvents;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EventReservation.Infrastructure.Messaging.Publishers;

public sealed class RabbitMqPublisher(
    IConnection connection,
    IOptions<RabbitMqPublisherSettings> settings) : IEventPublisher
{
    private readonly RabbitMqPublisherSettings _settings = settings.Value;

    public async Task<Result> PublishAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        var routingKey = typeof(TEvent).Name;

        var result = await Success(@event)
            .MapTry(SerializePayload, ex => Errors.SerializationFailed(ex.Message))
            .Bind(payload => SendToBrokerAsync(routingKey, payload, cancellationToken));

        return result;
    }

    private async Task<Result> SendToBrokerAsync(
        string routingKey,
        byte[] body,
        CancellationToken cancellationToken)
    {
        var result = await Try(async () =>
        {
            using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                Headers = new Dictionary<string, object?>()
            };

            TraceContextPropagation.InjectTraceContext(properties.Headers, Activity.Current);

            await channel.BasicPublishAsync(
                exchange: _settings.ExchangeName,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

        }, ex => Errors.BrokerPublishFailed(ex.Message));

        return result;
    }

    private static byte[] SerializePayload<TEvent>(TEvent @event)
        where TEvent : class, IIntegrationEvent
    {
        var json = JsonSerializer.Serialize(@event);
        var bytes = Encoding.UTF8.GetBytes(json);

        return bytes;
    }

    public static class Errors
    {
        private const string Context = "EVENT_PUBLISHER";

        public static ResultError SerializationFailed(string details) =>
            new(Context, $"Failed to serialize event payload: {details}", ErrorCategory.Unexpected);

        public static ResultError BrokerPublishFailed(string details) =>
            new(Context, $"Failed to publish message to RabbitMQ broker: {details}", ErrorCategory.Unavailable);
    }
}