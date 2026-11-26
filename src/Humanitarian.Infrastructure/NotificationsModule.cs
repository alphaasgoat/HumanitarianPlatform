using Humanitarian.Application.Common;
using Humanitarian.Application.Notifications;
using Humanitarian.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace Humanitarian.Infrastructure;

/// <summary>Notifications: event handler (Observer), outbox, channels (Strategy + Factory) and the dispatcher.</summary>
public class NotificationsModule : IPlatformModule
{
    public void Register(IServiceCollection services)
    {
        services.AddOptions<EmailSettings>().BindConfiguration(EmailSettings.Section);

        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationDispatchService, NotificationDispatchService>();

        // Strategy: one class per channel. Factory: picks the right one by channel.
        services.AddSingleton<INotificationChannel, EmailChannel>();
        services.AddSingleton<INotificationChannel, SimulatedSmsChannel>();
        services.AddSingleton<INotificationChannelFactory, NotificationChannelFactory>();

        // Observer / pub-sub: services publish events, the subscriber below turns them into notifications.
        services.AddScoped<IDomainEventPublisher, InProcessDomainEventPublisher>();
        services.AddScoped<NotificationEventHandler>();
        services.AddScoped<IDomainEventHandler<RequestStatusChanged>>(sp => sp.GetRequiredService<NotificationEventHandler>());
        services.AddScoped<IDomainEventHandler<ShelterPlacementMade>>(sp => sp.GetRequiredService<NotificationEventHandler>());
        services.AddScoped<IDomainEventHandler<VolunteerAssigned>>(sp => sp.GetRequiredService<NotificationEventHandler>());
    }
}
