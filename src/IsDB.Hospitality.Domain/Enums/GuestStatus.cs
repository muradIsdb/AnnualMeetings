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
    AirportReceptionSpecialist,
    TransportationSpecialist,
    AccommodationSpecialist,
    ControlRoom,
    Administrator
}

public enum VehicleStatus
{
    Available = 0,
    Assigned = 1,
    EnRoute = 2,
    Completed = 3,
    Maintenance = 4
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

public enum SyncStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2
}
