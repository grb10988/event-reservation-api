using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EventReservation.Application.Abstractions.IntegrationEvents;
using EventReservation.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EventReservation.Infrastructure.Messaging.Consumers;

public sealed class RabbitMqConsumerWorker<TEvent>(
    IConnectionFactory connectionFactory,
    IIntegrationEventDispatcher dispatcher,
    IOptionsMonitor<RabbitMqConsumerSettings> settingsMonitor,
    ILogger<RabbitMqConsumerWorker<TEvent>> logger) : BackgroundService
    where TEvent : class, IIntegrationEvent
{
    private static readonly ActivitySource ActivitySource = new("EventReservation.Application.IntegrationEvents.Consumers");

    private readonly IConnectionFactory _connectionFactory = connectionFactory;
    private readonly IIntegrationEventDispatcher _dispatcher = dispatcher;
    private readonly RabbitMqConsumerSettings _settings = settingsMonitor.Get(typeof(TEvent).Name);
    private readonly ILogger<RabbitMqConsumerWorker<TEvent>> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await CreateChannelAsync(cancellationToken)
            .Bind(channel => SetUpTopologyAsync(channel, cancellationToken))
            .Bind(channel => RegisterConsumerAsync(channel, cancellationToken))
            .Bind(() => AwaitCancellationAsync(cancellationToken))
            .TapError(errors =>
                _logger.LogError("Consumer worker failed to execute: {Errors}", errors.ToFailureMessages()));
    }

    private async Task<Result<IChannel>> CreateChannelAsync(CancellationToken cancellationToken)
    {
        var result = await Try(
            async () =>
            {
                var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken: cancellationToken);
                return await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            },
            ex => Errors.ChannelCreationFailed(ex.Message));

        return result;
    }

    private async Task<Result<IChannel>> SetUpTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        var routingKey = typeof(TEvent).Name;

        var result = await Try(async () =>
        {
            await channel.ExchangeDeclareAsync(
                _settings.ExchangeName,
                ExchangeType.Topic,
                durable: true,
                cancellationToken: cancellationToken);

            await channel.ExchangeDeclareAsync(
                _settings.DeadLetterExchange,
                ExchangeType.Direct,
                durable: true,
                cancellationToken: cancellationToken);

            Dictionary<string, object?> queueArgs = new()
            {
                { "x-dead-letter-exchange", _settings.DeadLetterExchange },
                { "x-dead-letter-routing-key", $"{routingKey}.dead-letter" }
            };

            await channel.QueueDeclareAsync(
                _settings.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs,
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                _settings.DeadLetterQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                _settings.QueueName,
                _settings.ExchangeName,
                routingKey,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                _settings.DeadLetterQueue,
                _settings.DeadLetterExchange,
                $"{routingKey}.dead-letter",
                cancellationToken: cancellationToken);

            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: _settings.PrefetchCount,
                global: false,
                cancellationToken: cancellationToken);

            return channel;

        }, ex => Errors.TopologySetupFailed(ex.Message));

        return result;
    }

    private async Task<Result<IChannel>> RegisterConsumerAsync(IChannel channel, CancellationToken cancellationToken)
    {
        var result = await Try(async () =>
        {
            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += (_, ea) => OnMessageReceivedAsync(channel, ea, cancellationToken);

            await channel.BasicConsumeAsync(
                queue: _settings.QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);

            return channel;

        }, ex => Errors.ConsumerRegistrationFailed(ex.Message));

        return result;
    }

    private async Task OnMessageReceivedAsync(
        IChannel channel,
        BasicDeliverEventArgs ea,
        CancellationToken cancellationToken)
    {
        var name = $"{typeof(TEvent).Name} (DeliveryTag {ea.DeliveryTag})";
        var stopwatch = Stopwatch.StartNew();

        PipelineLogging.LogStart(_logger, name);

        try
        {
            var result = await HandleMessageReceivedAsync(channel, ea, cancellationToken);
            stopwatch.Stop();
            PipelineLogging.LogCompleted(_logger, name, result, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogCritical(
                ex,
                "Unexpected, unhandled exception processing {Name} after {ElapsedMilliseconds:F2}ms",
                name,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static async Task<Result> AwaitCancellationAsync(CancellationToken cancellationToken)
    {
        var result = await TryCancelable(
            async () => await Task.Delay(Timeout.Infinite, cancellationToken),
            ex => Errors.HostExecutionFaulted(ex.Message));
        return result;
    }

    private async Task<Result> HandleMessageReceivedAsync(
        IChannel channel,
        BasicDeliverEventArgs ea,
        CancellationToken cancellationToken)
    {
        var result = await ProcessMessageAsync(ea, cancellationToken)
            .Tap(() => AcknowledgeMessageAsync(channel, ea.DeliveryTag, cancellationToken))
            .TapError(_ => RejectMessageAsync(channel, ea.DeliveryTag, cancellationToken));

        return result;
    }

    private async Task<Result> ProcessMessageAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        var routingKey = typeof(TEvent).Name;

        var parentContext = TraceContextPropagation.ExtractTraceContext(ea.BasicProperties.Headers)
            .Match(
                onSuccess: ctx => (ActivityContext?)ctx,
                onFailure: _ => null);

        using var activity = ActivitySource.StartActivity(
            $"process {routingKey}",
            ActivityKind.Consumer,
            parentContext ?? default);

        var result = await Success(ea.Body)
            .Ensure(body => !body.IsEmpty, Errors.EmptyPayload)
            .BindTry(
                body => DeserializePayload(body) is { } evt
                    ? Success(evt)
                    : Failure<TEvent>(Errors.NullDeserializedEvent),
                ex => Errors.DeserializationFailed(ex.Message))
            .Bind(evt => _dispatcher.DispatchAsync(evt, cancellationToken))
            .Tap(() => activity?.SetStatus(ActivityStatusCode.Ok))
            .TapError(errors =>
            {
                var failureMessage = errors.ToFailureMessages();
                activity?.SetStatus(ActivityStatusCode.Error, failureMessage);
                activity?.SetTag("error.type", "messaging_consume_failure");
            });

        return result;
    }

    private static TEvent? DeserializePayload(ReadOnlyMemory<byte> body)
    {
        var json = Encoding.UTF8.GetString(body.Span);
        var @event = JsonSerializer.Deserialize<TEvent>(json);

        return @event;
    }

    private async Task AcknowledgeMessageAsync(
        IChannel channel,
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        try
        {
            await channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to acknowledge message. DeliveryTag: {DeliveryTag}",
                deliveryTag);
        }
    }

    private async Task RejectMessageAsync(
        IChannel channel,
        ulong deliveryTag,
        CancellationToken cancellationToken)
    {
        try
        {
            await channel.BasicNackAsync(
                deliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to reject (nack) message. DeliveryTag: {DeliveryTag}",
                deliveryTag);
        }
    }

    public static class Errors
    {
        private const string Context = "CONSUMER";

        public static ResultError EmptyPayload =>
            new(Context, "Message body received from queue is empty.", ErrorCategory.Validation);

        public static ResultError NullDeserializedEvent =>
            new(Context, $"Deserialization resulted in a null {typeof(TEvent).Name} payload.", ErrorCategory.Validation);

        public static ResultError DeserializationFailed(string details) =>
            new(Context, $"Failed to deserialize payload into {typeof(TEvent).Name}: {details}", ErrorCategory.Validation);

        public static ResultError TopologySetupFailed(string details) =>
            new(Context, $"Failed to establish RabbitMQ topology: {details}", ErrorCategory.Unavailable);

        public static ResultError ChannelCreationFailed(string details) =>
        new(Context, $"Failed to create channel for worker: {details}", ErrorCategory.Unavailable);

        public static ResultError ConsumerRegistrationFailed(string details) =>
            new(Context, $"Failed to register event consumer: {details}", ErrorCategory.Unavailable);

        public static ResultError HostExecutionFaulted(string details) =>
            new(Context, $"Consumer host runtime encountered a fault: {details}", ErrorCategory.Unexpected);
    }
}