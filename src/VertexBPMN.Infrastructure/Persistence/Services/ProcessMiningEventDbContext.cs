using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.Persistence.Services
{
    public class ProcessMiningEventDbContext : DbContext
    {
        public DbSet<ProcessMiningEvent> Events { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public ProcessMiningEventDbContext(DbContextOptions<ProcessMiningEventDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<ProcessMiningEvent>();
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ProcessInstanceId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.TaskId).HasMaxLength(100);
            entity.Property(e => e.ActivityId).HasMaxLength(100);
            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.TenantId).HasMaxLength(64);
            entity.Property(e => e.PayloadJson).HasMaxLength(4000);

            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.ProcessInstanceId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.Timestamp);

            var audit = modelBuilder.Entity<AuditLog>();
            audit.HasKey(e => e.Id);
            audit.Property(e => e.Action).HasMaxLength(200).IsRequired();
            audit.Property(e => e.Resource).HasMaxLength(300);
            audit.Property(e => e.ResourceId).HasMaxLength(200);
            audit.Property(e => e.UserId).HasMaxLength(200);
            audit.Property(e => e.TenantId).HasMaxLength(64);
            audit.Property(e => e.CorrelationId).HasMaxLength(128);
            audit.Property(e => e.DetailsJson).HasMaxLength(8000);
            audit.HasIndex(e => e.Timestamp);
            audit.HasIndex(e => new { e.TenantId, e.Timestamp });
            audit.HasIndex(e => e.Action);

            // Seed sample events (if empty DB after first migration)
            var seedTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            entity.HasData(
                new ProcessMiningEvent
                {
                    Id = 1,
                    EventType = "PROCESS_STARTED",
                    ProcessInstanceId = "33333333-3333-3333-3333-333333333333",
                    TaskId = null,
                    ActivityId = "startEvent1",
                    UserId = "system",
                    TenantId = "tenant-default",
                    Timestamp = seedTime,
                    PayloadJson = null
                },
                new ProcessMiningEvent
                {
                    Id = 2,
                    EventType = "TASK_CREATED",
                    ProcessInstanceId = "33333333-3333-3333-3333-333333333333",
                    TaskId = "55555555-5555-5555-5555-555555555555",
                    ActivityId = "activity_userTask_1",
                    UserId = null,
                    TenantId = "tenant-default",
                    Timestamp = seedTime.AddMinutes(1),
                    PayloadJson = "{\"name\":\"Review Request\"}"
                }
            );

            base.OnModelCreating(modelBuilder);
        }
    }
}
