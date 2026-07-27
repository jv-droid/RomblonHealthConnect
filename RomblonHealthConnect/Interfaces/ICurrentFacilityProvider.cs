namespace RomblonHealthConnect.Interfaces;

/// <summary>
/// Resolves which facility the current user is acting for. This drives the
/// Incoming and Outgoing queues and the notification centre.
/// Phase 3 has no authentication yet, so the facility is held in session and
/// defaults to the provincial hospital. Replace with a claim once sign-in lands.
/// </summary>
public interface ICurrentFacilityProvider
{
    Task<int> GetHospitalIdAsync(CancellationToken cancellationToken = default);

    void SetHospitalId(int hospitalId);
}
