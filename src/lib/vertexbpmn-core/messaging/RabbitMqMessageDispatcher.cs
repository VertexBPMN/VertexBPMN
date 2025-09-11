using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using VertexBPMN.Core.Cmmn;
using VertexBPMN.Core.Domain;

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

        public Task DispatchServiceTaskAsync(string targetWorkerId, string implementation, Dictionary<string, string> attributes, Dictionary<string, object> variables,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishCaseTokenAsync(CaseToken token, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task QueueTaskAsync(string taskId, string taskType, Dictionary<string, object> variables,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DispatchUserTaskAsync(string assignee, string taskId, Dictionary<string, object> variables,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, object>> DispatchDmnTaskAsync(string targetWorker, string decisionRef, Dictionary<string, object> variables,
            CancellationToken cancellationToken = default)
        {
            return null;
        }

        public Task SubscribeToMessageAsync(string messageName, Func<Message, Task> handler, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishCaseFileUpdateAsync(CaseFileUpdateEvent updateEvent, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SubscribeToCaseFileUpdateAsync(string caseId, Func<CaseFileUpdateEvent, Task> handler, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
