using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace VertexBPMN.Core.Messaging
{
    public class RabbitMqMessageDispatcher : IMessageDispatcher
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;

        public RabbitMqMessageDispatcher(string rabbitMqConnectionString)
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(rabbitMqConnectionString)
            };

            _connection = factory.CreateConnectionAsync().Result;;
            _channel = _connection.CreateChannelAsync().Result;

            // Optional: Declare a default exchange/queue
            _channel.ExchangeDeclareAsync(exchange: "service_tasks", type: ExchangeType.Direct, durable: true);
        }

        public async Task DispatchServiceTaskAsync(
            string targetWorkerId,
            string implementation,
            IDictionary<string, string> attributes,
            IDictionary<string, object> variables,
            CancellationToken ct = default)
        {
            // Prepare the message payload
            var message = new
            {
                TargetWorkerId = targetWorkerId,
                Implementation = implementation,
                Attributes = attributes,
                Variables = variables
            };

            var messageBody = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(messageBody);

            // Publish the message to the RabbitMQ exchange
            var properties = new BasicProperties();
            properties.ContentType = "text/plain";
            properties.DeliveryMode =  DeliveryModes.Persistent;


            await _channel.BasicPublishAsync(
                exchange: "service_tasks",
                routingKey: targetWorkerId, // Use the worker ID as the routing key
                mandatory: true,
                basicProperties: properties,
                body: body);
        }

        public void Dispose()
        {
            _channel?.CloseAsync();
            _connection?.CloseAsync();
        }
    }
}
