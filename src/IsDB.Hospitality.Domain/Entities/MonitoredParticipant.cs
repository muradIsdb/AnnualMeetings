using IsDB.Hospitality.Domain.Common;

namespace IsDB.Hospitality.Domain.Entities;

/// <summary>
/// A name or email that Admin has added to the departure shuttle watch list.
/// When a participant submits the departure shuttle form and their name/email
/// matches an entry here, a SyncAlert is automatically created.
/// </summary>
public class MonitoredParticipant : BaseEntity
{
    /// <summary>The name or email to monitor (case-insensitive).</summary>
    public string NameOrEmail { get; set; } = string.Empty;

    /// <summary>True = exact match required; False = contains/partial match.</summary>
    public bool IsExactMatch { get; set; } = false;

    /// <summary>Username of the admin who added this entry.</summary>
    public string AddedByUserName { get; set; } = string.Empty;

    /// <summary>When this entry was added.</summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
