using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Cmn;

namespace VertexBPMN.Application.Messaging
{
    public class RabbitMqMessageDispatcher : IMessageDispatcher
    {
        private readonly string _rabbitMqConnectionString;
        private IConnection? _connection;
        private IChannel? _channel;

        public RabbitMqMessageDispatcher(string rabbitMqConnectionString)
        {
            if (string.IsNullOrWhiteSpace(rabbitMqConnectionString))
                throw new ArgumentException("RabbitMQ connection string is required", nameof(rabbitMqConnectionString));

            _rabbitMqConnectionString = rabbitMqConnectionString;
        }

        private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
        {
            if (_channel is not null)
                return _channel;

            var factory = new ConnectionFactory { Uri = new Uri(_rabbitMqConnectionString) };
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync();
            await _channel.ExchangeDeclareAsync("service_tasks", ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
            return _channel;
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


            var channel = await GetChannelAsync(ct);
            await channel.BasicPublishAsync(
                exchange: "service_tasks",
                routingKey: targetWorkerId, // Use the worker ID as the routing key
                mandatory: true,
                basicProperties: properties,
                body: body);
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }

        public Task DispatchServiceTaskAsync(string targetWorkerId, string implementation, Dictionary<string, string> attributes, Dictionary<string, object> variables,
            CancellationToken cancellationToken = default)
        {
            return DispatchServiceTaskAsync(targetWorkerId, implementation, attributes, variables, cancellationToken);
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
            throw new NotSupportedException("RabbitMQ DMN dispatch requires a response-consumer contract.");
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

        public Task DispatchAiTaskAsync(string targetWorkerId, string aiProvider, string aiModel, Dictionary<string, string> attributes,
            Dictionary<string, object> variables, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
