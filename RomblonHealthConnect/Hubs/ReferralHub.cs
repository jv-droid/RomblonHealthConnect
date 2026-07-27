using Microsoft.AspNetCore.SignalR;

namespace RomblonHealthConnect.Hubs;

/// <summary>
/// Real-time channel for referral activity. Clients join a group per facility so a
/// status change only reaches the two hospitals involved in the transfer.
/// </summary>
public class ReferralHub : Hub
{
    /// <summary>Client event names, kept here so the server and referral-realtime.js cannot drift.</summary>
    public static class Events
    {
        public const string ReferralCreated = "referralCreated";
        public const string ReferralStatusChanged = "referralStatusChanged";
        public const string NotificationReceived = "notificationReceived";
        public const string CountsChanged = "countsChanged";
    }

    public static string GroupForHospital(int hospitalId) => $"hospital-{hospitalId}";

    /// <summary>Subscribes the caller to a facility's referral traffic.</summary>
    public async Task JoinHospitalGroup(int hospitalId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupForHospital(hospitalId));
    }

    public async Task LeaveHospitalGroup(int hospitalId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupForHospital(hospitalId));
    }
}
