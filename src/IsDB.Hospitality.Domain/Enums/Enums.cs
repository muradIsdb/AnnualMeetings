namespace IsDB.Hospitality.Domain.Enums;
public enum GuestStatus
{
    Expected = 0,
    ArrivedAtAirport = 1,
    PassedPassportControl = 2,
    LuggageReceived = 3,
    ReceivedByEmbassy = 4,
    OnTheWayToHotel = 5,
    AtHotel = 6,
    DepartingHotel = 7,
    AtAirportDeparture = 8,
    Departed = 9
}
public enum AlertSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
public enum UserRole
{
    Admin = 0,
    Airport = 1,
    Transport = 2,
    Hotel = 3,
    ControlRoom = 4,
    Liaison = 5
}
public enum VehicleStatus
{
    Available = 0,
    Assigned = 1,
    OutOfService = 2
}
public enum DriverStatus
{
    Available = 0,
    Assigned = 1,
    OffDuty = 2
}
public enum AssignmentType
{
    DropOff = 0,
    Dedicated = 1
}
public enum FlightStatus
{
    Scheduled = 0,
    Active = 1,
    Landed = 2,
    Cancelled = 3,
    Diverted = 4,
    Unknown = 5
}
public enum ChecklistItemType
{
    ArrivalAirport,
    Departure
}
public enum TravelBookingStatus
{
    NewBooking = 0,
    Amended = 1,
    Cancellation = 2,
    ConfirmedBooking = 3,
    ConfirmedCancellation = 4
}

public enum SyncStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2
}

/// <summary>Inbound journey status (Airport → Hotel)</summary>
public enum InboundStatus
{
    /// <summary>Default — guest is registered and expected</summary>
    ArrivalScheduled = 0,
    /// <summary>Guest flight has landed; set by Airport/Admin</summary>
    Arrived = 1,
    /// <summary>Embassy team has received the guest; set by Airport/Admin (independent of VehicleAssigned)</summary>
    ReceivedByEmbassyTeam = 2,
    /// <summary>Vehicle assigned and guest left airport; set automatically when vehicle is assigned</summary>
    VehicleAssigned = 3,
    /// <summary>Guest has checked in at hotel; set by Hotel/Admin (requires Arrived + at least one of ReceivedByEmbassyTeam or VehicleAssigned)</summary>
    AtHotel = 4
}

/// <summary>Outbound journey status (Hotel → Departure). Unlocked when InboundStatus reaches AtHotel.</summary>
public enum OutboundStatus
{
    /// <summary>Guest is at hotel; inherited automatically from InboundStatus.AtHotel</summary>
    AtHotel = 0,
    /// <summary>Guest left hotel en route to airport; set by Hotel/Admin</summary>
    InTransferToAirport = 1,
    /// <summary>Guest arrived at departure terminal; set by Airport/Transport/Admin</summary>
    AtAirport = 2,
    /// <summary>Guest has boarded departure flight; set by Airport/Admin</summary>
    BoardingCompleted = 3
}

/// <summary>Which journey track a status history entry belongs to</summary>
public enum StatusTrack
{
    Inbound = 0,
    Outbound = 1,
    /// <summary>Vehicle activity events (assign, unassign, reassign, force-assign)</summary>
    Vehicle = 2
}
