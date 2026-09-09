using EventReservation.Infrastructure.Messaging.Publishers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EventReservation.Infrastructure.Messaging;

public sealed class RabbitMqTopologyInitializer(
    IConnectionFactory connectionFactory,
    IOptions<RabbitMqPublisherSettings> settings) : IHostedService
{
    private readonly RabbitMqPublisherSettings _settings = settings.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken: cancellationToken);
        using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _settings.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

