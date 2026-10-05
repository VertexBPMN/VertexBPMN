using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.SourceControl;

internal static class SourceControlMapping
{
    public static void Configure(ModelBuilder model)
    {
        var binding = model.Entity<SourceControlBindingRecord>();
        binding.ToTable("SourceControlBindings");
        binding.HasKey(x => x.Id);
        binding.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
        binding.Property(x => x.Revision).IsConcurrencyToken();
        binding.Property(x => x.BindingJson).IsRequired();
        binding.Property(x => x.GrantsJson).IsRequired();
        binding.HasIndex(x => new { x.TenantId, x.Id }).IsUnique();

        var session = model.Entity<SourceControlSessionRecord>();
        session.ToTable("SourceControlSessions");
        session.HasKey(x => x.Id);
        session.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
        session.Property(x => x.ActorId).HasMaxLength(512).IsRequired();
        session.Property(x => x.BaseCommit).HasMaxLength(64).IsRequired();
        session.Property(x => x.Revision).IsConcurrencyToken();
        session.HasIndex(x => new { x.TenantId, x.RepositoryId, x.ExpiresUtcTicks });

        var job = model.Entity<SourceControlOperationRecord>();
        job.ToTable("SourceControlOperations");
        job.HasKey(x => x.Id);
        job.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
        job.Property(x => x.ActorId).HasMaxLength(512).IsRequired();
        job.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        job.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        job.Property(x => x.LeaseOwner).HasMaxLength(128);
        job.Property(x => x.Fence).IsConcurrencyToken();
        job.HasIndex(x => new { x.TenantId, x.RepositoryId, x.Kind, x.IdempotencyKey }).IsUnique();
        job.HasIndex(x => new { x.State, x.LeaseUntilUtcTicks });
    }
}
