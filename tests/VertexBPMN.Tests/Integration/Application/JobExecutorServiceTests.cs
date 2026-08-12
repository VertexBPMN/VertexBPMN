using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Application;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Integration.Application;

public sealed class JobExecutorServiceTests
{
    [Fact]
    public async Task PollingIteration_DispatchesPayloadAndDeletesSuccessfulJob()
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = Guid.NewGuid(),
            Type = "test-handler",
            DueDate = DateTime.UtcNow,
            Payload = "{\"attributes\":{\"operation\":\"approve\"},\"variables\":{\"amount\":42}}"
        };
        var repository = new RecordingJobRepository(job);
        var handler = new RecordingHandler();
        var registry = new ServiceTaskRegistry();
        registry.Register("test-handler", handler);

        using var services = new ServiceCollection()
            .AddSingleton<IJobRepository>(repository)
            .BuildServiceProvider();
        var executor = new JobExecutorService(
            services,
            NullLogger<JobExecutorService>.Instance,
            registry);

        await executor.RunPollingIterationAsync(CancellationToken.None);

        Assert.Equal("approve", handler.Attributes["operation"]);
        Assert.Equal(42, handler.Variables["amount"] is System.Text.Json.JsonElement amount
            ? amount.GetInt32()
            : handler.Variables["amount"]);
        Assert.Equal(job.Id, repository.DeletedJobId);
    }

    private sealed class RecordingHandler : IServiceTaskHandler
    {
        public IDictionary<string, string> Attributes { get; private set; } = new Dictionary<string, string>();
        public IDictionary<string, object> Variables { get; private set; } = new Dictionary<string, object>();

        public Task ExecuteAsync(IDictionary<string, string> attributes, IDictionary<string, object> variables, CancellationToken ct = default)
        {
            Attributes = new Dictionary<string, string>(attributes);
            Variables = new Dictionary<string, object>(variables);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingJobRepository(Job job) : IJobRepository
    {
        public Guid? DeletedJobId { get; private set; }

        public ValueTask AddAsync(Job value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<Job?>(id == job.Id ? job : null);

        public async IAsyncEnumerable<Job> ListDueAsync(DateTime asOf, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (job.DueDate <= asOf)
                yield return job;

            await Task.CompletedTask;
        }

        public ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeletedJobId = id;
            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(Job value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}