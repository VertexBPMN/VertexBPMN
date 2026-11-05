using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.Persistence;

internal static class EventStreamModel
{
    internal static void Configure(ModelBuilder model)
    {
        var sub = model.Entity<EventStreamSubscription>();
        sub.HasKey(x => x.Id);
        sub.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
        sub.Property(x => x.Name).HasMaxLength(255).IsRequired();
        sub.HasAlternateKey(x => new { x.Id, x.TenantId });
        var delivery = model.Entity<EventStreamDelivery>();
        delivery.HasKey(x => x.Id);
        delivery.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
        delivery.Property(x => x.EventType).HasMaxLength(128).IsRequired();
        delivery.HasIndex(x => new { x.SubscriptionId, x.SourceEventId }).IsUnique();
        delivery.HasOne<EventStreamSubscription>().WithMany()
            .HasForeignKey(x => new { x.SubscriptionId, x.TenantId })
            .HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Cascade);
    }
}
