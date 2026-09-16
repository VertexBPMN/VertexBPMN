using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.Persistence;

internal static class ExternalTaskMapping
{
    public static void Configure(ModelBuilder model)
    {
        var job = model.Entity<ExternalTaskJob>();
        job.ToTable("ExternalTaskJobs", table =>
        {
            table.HasCheckConstraint("CK_ExternalTaskJobs_Attempts", "\"MaxAttempts\" >= 1 AND \"MaxAttempts\" <= 10 AND \"AttemptsStarted\" >= 0 AND \"AttemptsStarted\" <= \"MaxAttempts\"");
            table.HasCheckConstraint("CK_ExternalTaskJobs_Time", "\"Deadline\" > \"CreatedAt\" AND \"AvailableAt\" >= \"CreatedAt\"");
            table.HasCheckConstraint("CK_ExternalTaskJobs_Revision", "\"Revision\" >= 0 AND \"LeaseGeneration\" >= 0");
            table.HasCheckConstraint("CK_ExternalTaskJobs_State", "\"State\" IN ('Ready','Leased','RetryScheduled','Completed','Failed','Cancelled','TimedOut')");
            table.HasCheckConstraint("CK_ExternalTaskJobs_Lease", "\"State\" <> 'Leased' OR (\"LeaseId\" IS NOT NULL AND \"LeaseExpiresAt\" IS NOT NULL AND \"WorkerIssuer\" IS NOT NULL AND \"WorkerSubject\" IS NOT NULL AND \"LeaseGeneration\" > 0 AND \"AttemptsStarted\" > 0 AND \"LeaseExpiresAt\" <= \"Deadline\")");
        });
        job.HasKey(x => x.Id);
        job.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
        job.Property(x => x.ActivityId).HasMaxLength(255).IsRequired();
        job.Property(x => x.Topic).HasMaxLength(128).IsRequired();
        job.Property(x => x.ContractVersion).HasMaxLength(128).IsRequired();
        job.Property(x => x.AgentProfileRef).HasMaxLength(128);
        job.Property(x => x.AgentProfileVersion).HasMaxLength(128);
        job.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
        job.Property(x => x.Revision).IsConcurrencyToken();
        job.Property(x => x.ResultHash).HasMaxLength(64);
        job.Property(x => x.ErrorCode).HasMaxLength(128);
        job.HasIndex(x => new { x.TenantId, x.ActivityExecutionId }).IsUnique();
        job.HasIndex(x => x.WaitTokenId).IsUnique();
        job.HasIndex(x => new { x.TenantId, x.Topic, x.State, x.AvailableAt, x.Id });
        job.HasIndex(x => new { x.State, x.LeaseExpiresAt, x.Id });
        job.HasIndex(x => new { x.State, x.Deadline, x.Id });
        // Retention must explicitly drain external work before deleting its runtime owners.
        job.HasOne<ProcessInstance>().WithMany().HasForeignKey(x => x.ProcessInstanceId).OnDelete(DeleteBehavior.Restrict);
        job.HasOne<ExecutionToken>().WithMany().HasForeignKey(x => x.WaitTokenId).OnDelete(DeleteBehavior.Restrict);
        job.HasOne<ProcessDefinition>().WithMany().HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Restrict);

        var attempt = model.Entity<ExternalTaskAttempt>();
        attempt.HasKey(x => x.Id);
        attempt.HasIndex(x => new { x.JobId, x.AttemptNumber }).IsUnique();
        attempt.HasIndex(x => new { x.JobId, x.LeaseGeneration }).IsUnique();
        attempt.HasOne<ExternalTaskJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        attempt.Property(x => x.FailureHash).HasMaxLength(64);
        attempt.Property(x => x.ErrorCode).HasMaxLength(128);

        var continuation = model.Entity<ExternalTaskContinuation>();
        continuation.HasKey(x => x.Id);
        continuation.HasIndex(x => x.JobId).IsUnique();
        continuation.HasIndex(x => new { x.State, x.CreatedAt, x.Id });
        continuation.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
        continuation.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(32);
        continuation.Property(x => x.Revision).IsConcurrencyToken();
        continuation.HasOne<ExternalTaskJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
    }
}
