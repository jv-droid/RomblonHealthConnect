using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Hubs;
using RomblonHealthConnect.Interfaces;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Services;

/// <summary>
/// Writes notifications to the database and pushes them to the target facility's SignalR group.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ReferralHub> _hub;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        IHubContext<ReferralHub> hub,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hub = hub;
        _logger = logger;
    }

    public async Task CreateAsync(
        int hospitalId,
        NotificationType type,
        string title,
        string message,
        int? referralId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            HospitalId = hospitalId,
            Type = type,
            Title = title,
            Message = message,
            ReferralId = referralId,
            CreatedUtc = DateTime.UtcNow,
            IsRead = false
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        // Delivery failures must not roll back the referral transition that caused them.
        try
        {
            await _hub.Clients
                .Group(ReferralHub.GroupForHospital(hospitalId))
                .SendAsync(
                    ReferralHub.Events.NotificationReceived,
                    new
                    {
                        id = notification.Id,
                        type = notification.Type.ToString(),
                        title = notification.Title,
                        message = notification.Message,
                        referralId = notification.ReferralId,
                        icon = notification.Icon,
                        createdUtc = notification.CreatedUtc
                    },
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push notification {NotificationId} to hospital {HospitalId}.",
                notification.Id, hospitalId);
        }
    }

    public async Task<IReadOnlyList<Notification>> GetRecentAsync(
        int hospitalId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .Where(n => n.HospitalId == hospitalId)
            .OrderByDescending(n => n.CreatedUtc)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(int hospitalId, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .CountAsync(n => n.HospitalId == hospitalId && !n.IsRead, cancellationToken);
    }

    public async Task MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        await _context.Notifications
            .Where(n => n.Id == notificationId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);
    }

    public async Task MarkAllAsReadAsync(int hospitalId, CancellationToken cancellationToken = default)
    {
        await _context.Notifications
            .Where(n => n.HospitalId == hospitalId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);
    }
}
