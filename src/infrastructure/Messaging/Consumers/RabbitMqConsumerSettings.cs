namespace EventReservation.Infrastructure.Messaging.Consumers;

public sealed class RabbitMqConsumerSettings
{
    public const string SectionName = "RabbitMqConsumer";

    public required string QueueName { get; init; }
    public required string ExchangeName { get; init; }
    public required string DeadLetterExchange { get; init; }
    public required string DeadLetterQueue { get; init; }
    public ushort PrefetchCount { get; init; } = 10;
}