using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using VertexBPMN.Infrastructure.Messaging;

namespace VertexBPMN.Tests.Integration.Api;

internal static class Lc09PublishProbe
{
    internal static async Task PublishAsync(IServiceProvider services)
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new RuntimeOutboxOptions
        {
            Enabled = true, Provider = "RabbitMQ", Destination = "lc09-checkpoint",
            ConnectionString = Environment.GetEnvironmentVariable("LC09_TEST_RABBIT")
                ?? throw new InvalidOperationException("LC09_TEST_RABBIT required")
        };
        var factory = new ConnectionFactory { Uri = new Uri(options.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await channel.ExchangeDeclareAsync(options.Destination, ExchangeType.Topic, durable: true, cancellationToken: ct);
        var queue = await channel.QueueDeclareAsync(cancellationToken: ct);
        await channel.QueueBindAsync(queue.QueueName, options.Destination, "#", cancellationToken: ct);
        using var publisher = new RuntimeOutboxPublisherService(
            services.GetRequiredService<IServiceScopeFactory>(), new RabbitMqRuntimeOutboxTransport(options),
            options, NullLogger<RuntimeOutboxPublisherService>.Instance);
        Assert.Equal(2, await publisher.RunOnceAsync(ct));
        Assert.NotNull(await channel.BasicGetAsync(queue.QueueName, autoAck: true, cancellationToken: ct));
        Assert.NotNull(await channel.BasicGetAsync(queue.QueueName, autoAck: true, cancellationToken: ct));
    }
}
