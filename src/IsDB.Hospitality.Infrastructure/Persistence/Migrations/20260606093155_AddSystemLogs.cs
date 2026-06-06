using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsDB.Hospitality.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventTitle = table.Column<string>(type: "TEXT", nullable: false),
                    MinimumLeadTimeHours = table.Column<int>(type: "INTEGER", nullable: false),
                    LogRetentionDays = table.Column<int>(type: "INTEGER", nullable: false),
                    EventTimezone = table.Column<string>(type: "TEXT", nullable: false),
                    PlaCardTheme = table.Column<string>(type: "TEXT", nullable: false),
                    EventLogoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    EventLogoBase64 = table.Column<string>(type: "TEXT", nullable: true),
                    EventLogoMimeType = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AviationstackApiKey = table.Column<string>(type: "TEXT", nullable: true),
                    AviationstackSyncIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    AviationstackTrackingWindowHours = table.Column<int>(type: "INTEGER", nullable: false),
                    AviationstackDateGuardDays = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CarClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Color = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    EventCode = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarClasses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventsAirConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", nullable: false),
                    ClientSecret = table.Column<string>(type: "TEXT", nullable: false),
                    ApiBaseUrl = table.Column<string>(type: "TEXT", nullable: false),
                    TokenEndpoint = table.Column<string>(type: "TEXT", nullable: false),
                    EventCode = table.Column<string>(type: "TEXT", nullable: false),
                    TenantCode = table.Column<string>(type: "TEXT", nullable: false),
                    SyncIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoSyncEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncOnStartup = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "TEXT", nullable: false),
                    LastSyncMessage = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncRecordsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSyncDeactivatedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventsAirConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventsAirSyncLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    RecordsSynced = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    SyncType = table.Column<string>(type: "TEXT", nullable: false),
                    TriggerSource = table.Column<string>(type: "TEXT", nullable: false),
                    InitiatedByStaffId = table.Column<Guid>(type: "TEXT", nullable: true),
                    InitiatedByStaffName = table.Column<string>(type: "TEXT", nullable: true),
                    RecordsAdded = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordsUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordsDeactivated = table.Column<int>(type: "INTEGER", nullable: false),
                    TravelBookingsSynced = table.Column<int>(type: "INTEGER", nullable: false),
                    EventCode = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventsAirSyncLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Flights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlightNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AirlineName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AirlineIataCode = table.Column<string>(type: "TEXT", nullable: true),
                    ScheduledDeparture = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ScheduledArrival = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeparturePortName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DeparturePortIataCode = table.Column<string>(type: "TEXT", nullable: true),
                    ArrivalPortName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ArrivalPortIataCode = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualDeparture = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualArrival = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActualTerminal = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ActualGate = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    LastTrackedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LiveDelayMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FlightSyncLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TriggerSource = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    FlightsInWindow = table.Column<int>(type: "INTEGER", nullable: false),
                    FlightsQueried = table.Column<int>(type: "INTEGER", nullable: false),
                    FlightsUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    InitiatedByStaffName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightSyncLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HotelOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ContractedRoomsIsDB = table.Column<int>(type: "INTEGER", nullable: false),
                    ContractedRoomsGuest = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualOccupiedIsDB = table.Column<int>(type: "INTEGER", nullable: false),
                    ActualOccupiedGuest = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventKey = table.Column<string>(type: "TEXT", nullable: false),
                    EventLabel = table.Column<string>(type: "TEXT", nullable: false),
                    MessageTemplate = table.Column<string>(type: "TEXT", nullable: false),
                    TargetRoles = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickupDayOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickupDayOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickupHourOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickupHourOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrationTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsSelectedForSync = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFromEventsAir = table.Column<bool>(type: "INTEGER", nullable: false),
                    EventsAirId = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RefreshToken = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RefreshTokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncFieldMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    EventsAirFieldGuid = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    EventCode = table.Column<string>(type: "TEXT", nullable: true),
                    FieldRole = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncFieldMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServiceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RecordsProcessed = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordsUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CarClassRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RegistrationTypeName = table.Column<string>(type: "TEXT", nullable: false),
                    CarClassId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    EventCode = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarClassRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarClassRules_CarClasses_CarClassId",
                        column: x => x.CarClassId,
                        principalTable: "CarClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Guests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventsAirContactId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Organization = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Designation = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Nationality = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                    PassportNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PhotoUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    MobileNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsCritical = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresAccessibility = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RoomNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    HotelName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SpecialRequirements = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RegistrationTypeId = table.Column<string>(type: "TEXT", nullable: true),
                    RegistrationTypeName = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    EventCode = table.Column<string>(type: "TEXT", nullable: true),
                    RankValue = table.Column<string>(type: "TEXT", nullable: true),
                    VehicleTypeValue = table.Column<string>(type: "TEXT", nullable: true),
                    DedicatedCar = table.Column<string>(type: "TEXT", nullable: true),
                    DeservedCarClassId = table.Column<Guid>(type: "TEXT", nullable: true),
                    InboundStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedByEmbassyTeam = table.Column<bool>(type: "INTEGER", nullable: false),
                    OutboundStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Guests_CarClasses_DeservedCarClassId",
                        column: x => x.DeservedCarClassId,
                        principalTable: "CarClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DepartureRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RoomNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    HotelOptionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PickupDayOptionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PickupHourOptionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisclaimerAccepted = table.Column<bool>(type: "INTEGER", nullable: false),
                    ManageToken = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventCode = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartureRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartureRequests_HotelOptions_HotelOptionId",
                        column: x => x.HotelOptionId,
                        principalTable: "HotelOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartureRequests_PickupDayOptions_PickupDayOptionId",
                        column: x => x.PickupDayOptionId,
                        principalTable: "PickupDayOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartureRequests_PickupHourOptions_PickupHourOptionId",
                        column: x => x.PickupHourOptionId,
                        principalTable: "PickupHourOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    TargetRoles = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByStaffId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventCode = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_StaffUsers_CreatedByStaffId",
                        column: x => x.CreatedByStaffId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StaffUserRoles",
                columns: table => new
                {
                    StaffUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffUserRoles", x => new { x.StaffUserId, x.Role });
                    table.ForeignKey(
                        name: "FK_StaffUserRoles_StaffUsers_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Module = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: true),
                    RequestPath = table.Column<string>(type: "TEXT", nullable: true),
                    StaffUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StaffName = table.Column<string>(type: "TEXT", nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemLogs_StaffUsers_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SyncFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SyncFieldMappingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    IsSelectedForSync = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncFieldValues_SyncFieldMappings_SyncFieldMappingId",
                        column: x => x.SyncFieldMappingId,
                        principalTable: "SyncFieldMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    IsResolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolvedByStaffId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsSystemGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    TargetRole = table.Column<int>(type: "INTEGER", nullable: true),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alerts_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Alerts_StaffUsers_ResolvedByStaffId",
                        column: x => x.ResolvedByStaffId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ChecklistItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompletedByStaffId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistCompletions_ChecklistItems_ChecklistItemId",
                        column: x => x.ChecklistItemId,
                        principalTable: "ChecklistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChecklistCompletions_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChecklistCompletions_StaffUsers_CompletedByStaffId",
                        column: x => x.CompletedByStaffId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuestStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Track = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusValue = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusLabel = table.Column<string>(type: "TEXT", nullable: false),
                    ChangedByStaffId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ChangedByName = table.Column<string>(type: "TEXT", nullable: true),
                    ChangedByRole = table.Column<int>(type: "INTEGER", nullable: true),
                    IsSystemGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsRolledBack = table.Column<bool>(type: "INTEGER", nullable: false),
                    RolledBackByHistoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestStatusHistories_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuestStatusHistories_StaffUsers_ChangedByStaffId",
                        column: x => x.ChangedByStaffId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TravelBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FlightId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsArrival = table.Column<bool>(type: "INTEGER", nullable: false),
                    SeatClass = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BookingReference = table.Column<string>(type: "TEXT", nullable: true),
                    AirlineReference = table.Column<string>(type: "TEXT", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    BookingNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Itinerary = table.Column<string>(type: "TEXT", nullable: true),
                    Tickets = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Terminal = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Gate = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    DelayReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ChangedSinceLastView = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreviousFlightNumber = table.Column<string>(type: "TEXT", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TravelBookings_Flights_FlightId",
                        column: x => x.FlightId,
                        principalTable: "Flights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TravelBookings_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LicensePlate = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Make = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 30, nullable: true),
                    DriverName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DriverPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    BarcodeValue = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CarNumber = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    DriverId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CurrentGuestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CurrentAssignmentType = table.Column<int>(type: "INTEGER", nullable: true),
                    CarClassId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EventCode = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_CarClasses_CarClassId",
                        column: x => x.CarClassId,
                        principalTable: "CarClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Vehicles_Guests_CurrentGuestId",
                        column: x => x.CurrentGuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NotificationReads",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StaffUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationReads", x => new { x.NotificationId, x.StaffUserId });
                    table.ForeignKey(
                        name: "FK_NotificationReads_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationReads_StaffUsers_StaffUserId",
                        column: x => x.StaffUserId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TravelBookingHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TravelBookingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PreviousFlightNumber = table.Column<string>(type: "TEXT", nullable: false),
                    PreviousAirlineName = table.Column<string>(type: "TEXT", nullable: true),
                    PreviousScheduledArrival = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreviousScheduledDeparture = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreviousDeparturePort = table.Column<string>(type: "TEXT", nullable: true),
                    PreviousArrivalPort = table.Column<string>(type: "TEXT", nullable: true),
                    PreviousSeatClass = table.Column<string>(type: "TEXT", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelBookingHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TravelBookingHistories_TravelBookings_TravelBookingId",
                        column: x => x.TravelBookingId,
                        principalTable: "TravelBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    VehicleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    EventCode = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Drivers_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "VehicleStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VehicleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OldStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    NewStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ChangedByStaffId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ChangedByName = table.Column<string>(type: "TEXT", nullable: true),
                    ChangedByRole = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleStatusHistories_StaffUsers_ChangedByStaffId",
                        column: x => x.ChangedByStaffId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VehicleStatusHistories_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VehicleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedByStaffId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UnassignedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    EstimatedArrivalTime = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    AssignmentType = table.Column<int>(type: "INTEGER", nullable: false),
                    DriverId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UnassignedByStaffId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleAssignments_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VehicleAssignments_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VehicleAssignments_StaffUsers_AssignedByStaffId",
                        column: x => x.AssignedByStaffId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleAssignments_StaffUsers_UnassignedByStaffId",
                        column: x => x.UnassignedByStaffId,
                        principalTable: "StaffUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VehicleAssignments_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AppConfigs",
                columns: new[] { "Id", "AviationstackApiKey", "AviationstackDateGuardDays", "AviationstackSyncIntervalMinutes", "AviationstackTrackingWindowHours", "EventLogoBase64", "EventLogoMimeType", "EventLogoUrl", "EventTimezone", "EventTitle", "LogRetentionDays", "MinimumLeadTimeHours", "PlaCardTheme", "UpdatedAt" },
                values: new object[] { 1, null, 1, 5, 12, null, null, null, "Asia/Riyadh", "IsDB Annual Meetings 2025", 90, 2, "Light", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_GuestId",
                table: "Alerts",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_IsResolved",
                table: "Alerts",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_ResolvedByStaffId",
                table: "Alerts",
                column: "ResolvedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_CarClassRules_CarClassId",
                table: "CarClassRules",
                column: "CarClassId");

            migrationBuilder.CreateIndex(
                name: "IX_CarClassRules_RegistrationTypeName",
                table: "CarClassRules",
                column: "RegistrationTypeName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistCompletions_ChecklistItemId",
                table: "ChecklistCompletions",
                column: "ChecklistItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistCompletions_CompletedByStaffId",
                table: "ChecklistCompletions",
                column: "CompletedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistCompletions_GuestId_ChecklistItemId",
                table: "ChecklistCompletions",
                columns: new[] { "GuestId", "ChecklistItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartureRequests_Email",
                table: "DepartureRequests",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartureRequests_HotelOptionId",
                table: "DepartureRequests",
                column: "HotelOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartureRequests_ManageToken",
                table: "DepartureRequests",
                column: "ManageToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartureRequests_PickupDayOptionId",
                table: "DepartureRequests",
                column: "PickupDayOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartureRequests_PickupHourOptionId",
                table: "DepartureRequests",
                column: "PickupHourOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_VehicleId",
                table: "Drivers",
                column: "VehicleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Flights_ScheduledArrival",
                table: "Flights",
                column: "ScheduledArrival");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_Status",
                table: "Flights",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_DeservedCarClassId",
                table: "Guests",
                column: "DeservedCarClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_EventsAirContactId",
                table: "Guests",
                column: "EventsAirContactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guests_InboundStatus",
                table: "Guests",
                column: "InboundStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_IsActive",
                table: "Guests",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_IsCritical_LastName",
                table: "Guests",
                columns: new[] { "IsCritical", "LastName" });

            migrationBuilder.CreateIndex(
                name: "IX_Guests_Status",
                table: "Guests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GuestStatusHistories_ChangedByStaffId",
                table: "GuestStatusHistories",
                column: "ChangedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestStatusHistories_GuestId",
                table: "GuestStatusHistories",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_StaffUserId",
                table: "NotificationReads",
                column: "StaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedByStaffId",
                table: "Notifications",
                column: "CreatedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_EventKey",
                table: "NotificationTemplates",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffUsers_Email",
                table: "StaffUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncFieldValues_SyncFieldMappingId",
                table: "SyncFieldValues",
                column: "SyncFieldMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_Module_OccurredAt",
                table: "SystemLogs",
                columns: new[] { "Module", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_OccurredAt",
                table: "SystemLogs",
                column: "OccurredAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_Severity_OccurredAt",
                table: "SystemLogs",
                columns: new[] { "Severity", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogs_StaffUserId",
                table: "SystemLogs",
                column: "StaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelBookingHistories_TravelBookingId",
                table: "TravelBookingHistories",
                column: "TravelBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelBookings_FlightId",
                table: "TravelBookings",
                column: "FlightId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelBookings_GuestId",
                table: "TravelBookings",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelBookings_IsArrival",
                table: "TravelBookings",
                column: "IsArrival");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAssignments_AssignedByStaffId",
                table: "VehicleAssignments",
                column: "AssignedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAssignments_DriverId",
                table: "VehicleAssignments",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAssignments_GuestId",
                table: "VehicleAssignments",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAssignments_IsActive",
                table: "VehicleAssignments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAssignments_UnassignedByStaffId",
                table: "VehicleAssignments",
                column: "UnassignedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAssignments_VehicleId",
                table: "VehicleAssignments",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_BarcodeValue",
                table: "Vehicles",
                column: "BarcodeValue");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CarClassId",
                table: "Vehicles",
                column: "CarClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CurrentGuestId",
                table: "Vehicles",
                column: "CurrentGuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_LicensePlate",
                table: "Vehicles",
                column: "LicensePlate",
                unique: true,
                filter: "\"LicensePlate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleStatusHistories_ChangedByStaffId",
                table: "VehicleStatusHistories",
                column: "ChangedByStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleStatusHistories_VehicleId",
                table: "VehicleStatusHistories",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alerts");

            migrationBuilder.DropTable(
                name: "AppConfigs");

            migrationBuilder.DropTable(
                name: "CarClassRules");

            migrationBuilder.DropTable(
                name: "ChecklistCompletions");

            migrationBuilder.DropTable(
                name: "DepartureRequests");

            migrationBuilder.DropTable(
                name: "EventsAirConfigs");

            migrationBuilder.DropTable(
                name: "EventsAirSyncLogs");

            migrationBuilder.DropTable(
                name: "FlightSyncLogs");

            migrationBuilder.DropTable(
                name: "GuestStatusHistories");

            migrationBuilder.DropTable(
                name: "NotificationReads");

            migrationBuilder.DropTable(
                name: "NotificationTemplates");

            migrationBuilder.DropTable(
                name: "RegistrationTypes");

            migrationBuilder.DropTable(
                name: "StaffUserRoles");

            migrationBuilder.DropTable(
                name: "SyncFieldValues");

            migrationBuilder.DropTable(
                name: "SyncRecords");

            migrationBuilder.DropTable(
                name: "SystemLogs");

            migrationBuilder.DropTable(
                name: "TravelBookingHistories");

            migrationBuilder.DropTable(
                name: "VehicleAssignments");

            migrationBuilder.DropTable(
                name: "VehicleStatusHistories");

            migrationBuilder.DropTable(
                name: "ChecklistItems");

            migrationBuilder.DropTable(
                name: "HotelOptions");

            migrationBuilder.DropTable(
                name: "PickupDayOptions");

            migrationBuilder.DropTable(
                name: "PickupHourOptions");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "SyncFieldMappings");

            migrationBuilder.DropTable(
                name: "TravelBookings");

            migrationBuilder.DropTable(
                name: "Drivers");

            migrationBuilder.DropTable(
                name: "StaffUsers");

            migrationBuilder.DropTable(
                name: "Flights");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "Guests");

            migrationBuilder.DropTable(
                name: "CarClasses");
        }
    }
}
