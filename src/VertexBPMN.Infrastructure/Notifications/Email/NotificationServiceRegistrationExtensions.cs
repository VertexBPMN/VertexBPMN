using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Notifications.Email;

namespace VertexBPMN.Infrastructure.Notifications;

public static class NotificationServiceRegistrationExtensions
{
    public static IServiceCollection AddOptionalEmailNotifications(this IServiceCollection services, IConfiguration configuration, bool decorateExisting = true)
    {
        var section = configuration.GetSection("Notifications:Email");
        services.Configure<EmailNotificationOptions>(section);

        // Always register base email service (it no-ops if disabled)
        services.AddTransient<EmailNotificationService>();

        if (decorateExisting)
        {
            // If an INotificationService already exists, wrap it.
            var existing = services.FirstOrDefault(d => d.ServiceType == typeof(INotificationService));
            if (existing is not null)
            {
                services.AddTransient<INotificationService>(sp =>
                    new EmailNotificationDecoratingService(
                        (INotificationService)sp.GetRequiredService(existing.ImplementationType ?? existing.ServiceType),
                        sp.GetRequiredService<EmailNotificationService>(),
                        sp.GetRequiredService<ILogger<EmailNotificationDecoratingService>>()));
            }
            else
            {
                // Fallback: use email service directly
                services.AddTransient<INotificationService, EmailNotificationService>();
            }
        }
        else
        {
            services.AddTransient<INotificationService, EmailNotificationService>();
        }

        return services;
    }
}