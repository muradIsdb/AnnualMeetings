using System.Text;
using IsDB.Hospitality.Application;
using IsDB.Hospitality.Infrastructure;
using IsDB.Hospitality.Infrastructure.Persistence;
using IsDB.Hospitality.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// UI v2: placard layout, quick placard button, photo lightbox
var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// Application & Infrastructure layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IsDB.Hospitality.Application.Common.Interfaces.IEmailService, IsDB.Hospitality.API.Services.EmailService>();
builder.Services.AddScoped<IsDB.Hospitality.API.Services.NotificationTemplateService>();

// Register default HttpClient with SSL bypass for development/sandbox only
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpClient("").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });
}

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// In-memory cache for ActiveUserFilter (IsActive check)
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IsDB.Hospitality.API.Filters.ActiveUserFilter>();

builder.Services.AddControllers(options =>
{
    options.Conventions.Add(new Microsoft.AspNetCore.Mvc.ApplicationModels.RouteTokenTransformerConvention(
        new SlugifyParameterTransformer()));
    // Globally enforce IsActive check on every authenticated request
    options.Filters.AddService<IsDB.Hospitality.API.Filters.ActiveUserFilter>();
});
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "IsDB Hospitality Platform API",
        Version = "v1",
        Description = "Backend API for the IsDB VIP Guest Management System"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// Railway injects PORT env var — must be set before Build()
// Default to 5050 for local development (Railway always sets PORT=8080)
var port = Environment.GetEnvironmentVariable("PORT") ?? "5050";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

// Migrate and seed database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var isPostgres = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL"));
    if (isPostgres)
    {
        // Detect whether this is a fresh database or an existing legacy production database.
        // Fresh DB → run MigrateAsync() directly (migration chain is now idempotent).
        // Legacy production DB (has rows in __EFMigrationsHistory without InitialCreate) → run pre-creation block.
        var dbConn = context.Database.GetDbConnection();
        await dbConn.OpenAsync();
        bool isFreshDatabase;
        bool isEfCoreCreatedDb = false;
        using (var checkCmd = dbConn.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory'";
            var tableExists = Convert.ToInt64(await checkCmd.ExecuteScalarAsync()) > 0;
            long migrationCount = 0;
            if (tableExists)
            {
                checkCmd.CommandText = @"SELECT COUNT(*) FROM ""__EFMigrationsHistory""";
                migrationCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());
                if (migrationCount > 0)
                {
                    checkCmd.CommandText = @"SELECT COUNT(*) FROM ""__EFMigrationsHistory"" WHERE ""MigrationId"" LIKE '%InitialCreate%'";
                    isEfCoreCreatedDb = Convert.ToInt64(await checkCmd.ExecuteScalarAsync()) > 0;
                }
            }
            isFreshDatabase = migrationCount == 0;
        }
        await dbConn.CloseAsync();

        // ALWAYS ensure LicensePlate is nullable, regardless of which DB path is taken.
        // This runs before MigrateAsync() so the EF migration can succeed if it hasn't run yet.
        logger.LogInformation("Ensuring Vehicles.LicensePlate is nullable (idempotent pre-check)...");
        await context.Database.ExecuteSqlRawAsync(@"
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Vehicles' AND column_name = 'LicensePlate'
                    AND is_nullable = 'NO'
                ) THEN
                    ALTER TABLE ""Vehicles"" ALTER COLUMN ""LicensePlate"" DROP NOT NULL;
                END IF;
            END $$;
        ");
        logger.LogInformation("LicensePlate nullable pre-check complete.");

        if (isFreshDatabase || isEfCoreCreatedDb)
        {
            // Fresh or EF Core-managed database: run MigrateAsync() directly.
            // The migration chain is now idempotent (IF NOT EXISTS for duplicate tables).
            logger.LogInformation(isFreshDatabase
                ? "PostgreSQL detected (fresh database). Running all migrations from scratch..."
                : "PostgreSQL detected (EF Core-created database). Running pending migrations only...");
            await context.Database.MigrateAsync();
            logger.LogInformation("All migrations applied successfully.");
        }
        else
        {
        // PostgreSQL on Railway — pre-create CarClasses table with correct types BEFORE running migrations.
        // The EF Core migration was generated for SQLite and uses TEXT types, which fail on PostgreSQL.
        // By creating the table manually with correct types first, we prevent the migration from failing.
        logger.LogInformation("PostgreSQL detected (existing database). Pre-creating CarClasses table with correct types...");
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""CarClasses"" (
                ""Id""          uuid        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                ""Name""        text        NOT NULL,
                ""Description"" text        NULL,
                ""Color""       text        NULL,
                ""SortOrder""   integer     NOT NULL DEFAULT 0,
                ""CreatedAt""   timestamptz NOT NULL DEFAULT now(),
                ""UpdatedAt""   timestamptz NOT NULL DEFAULT now()
            );

            -- Add CarClassId to Vehicles if it doesn't exist
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Vehicles' AND column_name = 'CarClassId'
                ) THEN
                    ALTER TABLE ""Vehicles"" ADD COLUMN ""CarClassId"" uuid NULL
                        REFERENCES ""CarClasses""(""Id"") ON DELETE SET NULL;
                    CREATE INDEX IF NOT EXISTS ""IX_Vehicles_CarClassId"" ON ""Vehicles""(""CarClassId"");
                END IF;
            END $$;

            -- Add DeservedCarClassId to Guests if it doesn't exist
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Guests' AND column_name = 'DeservedCarClassId'
                ) THEN
                    ALTER TABLE ""Guests"" ADD COLUMN ""DeservedCarClassId"" uuid NULL
                        REFERENCES ""CarClasses""(""Id"") ON DELETE SET NULL;
                    CREATE INDEX IF NOT EXISTS ""IX_Guests_DeservedCarClassId"" ON ""Guests""(""DeservedCarClassId"");
                END IF;
            END $$;

            -- Mark the AddCarClassFeature migration as applied so EF Core won't try to run it
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260501060643_AddCarClassFeature', '9.0.0')
            ON CONFLICT DO NOTHING;

            -- AddGuestStatusFlow migration pre-creation
            -- Add InboundStatus to Guests if missing
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Guests' AND column_name = 'InboundStatus'
                ) THEN
                    ALTER TABLE ""Guests"" ADD COLUMN ""InboundStatus"" integer NOT NULL DEFAULT 0;
                END IF;
            END $$;

            -- Add ReceivedByEmbassyTeam to Guests if missing
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Guests' AND column_name = 'ReceivedByEmbassyTeam'
                ) THEN
                    ALTER TABLE ""Guests"" ADD COLUMN ""ReceivedByEmbassyTeam"" boolean NOT NULL DEFAULT false;
                END IF;
            END $$;

            -- Add OutboundStatus to Guests if missing
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Guests' AND column_name = 'OutboundStatus'
                ) THEN
                    ALTER TABLE ""Guests"" ADD COLUMN ""OutboundStatus"" integer NULL;
                END IF;
            END $$;

            -- Create GuestStatusHistories table if missing
            CREATE TABLE IF NOT EXISTS ""GuestStatusHistories"" (
                ""Id""                    uuid        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                ""GuestId""               uuid        NOT NULL REFERENCES ""Guests""(""Id"") ON DELETE CASCADE,
                ""Track""                 integer     NOT NULL,
                ""StatusValue""           integer     NOT NULL,
                ""StatusLabel""           text        NOT NULL,
                ""ChangedByStaffId""      uuid        NULL REFERENCES ""StaffUsers""(""Id"") ON DELETE SET NULL,
                ""ChangedByName""         text        NULL,
                ""ChangedByRole""         integer     NULL,
                ""IsSystemGenerated""     boolean     NOT NULL DEFAULT false,
                ""Notes""                 text        NULL,
                ""IsRolledBack""          boolean     NOT NULL DEFAULT false,
                ""RolledBackByHistoryId"" uuid        NULL,
                ""CreatedAt""             timestamptz NOT NULL DEFAULT now(),
                ""UpdatedAt""             timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ""IX_GuestStatusHistories_GuestId"" ON ""GuestStatusHistories""(""GuestId"");
            CREATE INDEX IF NOT EXISTS ""IX_GuestStatusHistories_ChangedByStaffId"" ON ""GuestStatusHistories""(""ChangedByStaffId"");

            -- Create VehicleStatusHistories table if missing
            CREATE TABLE IF NOT EXISTS ""VehicleStatusHistories"" (
                ""Id""                 uuid        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                ""VehicleId""          uuid        NOT NULL REFERENCES ""Vehicles""(""Id"") ON DELETE CASCADE,
                ""OldStatus""          integer     NOT NULL,
                ""NewStatus""          integer     NOT NULL,
                ""ChangedByStaffId""   uuid        NULL REFERENCES ""StaffUsers""(""Id"") ON DELETE SET NULL,
                ""ChangedByName""      text        NULL,
                ""ChangedByRole""      integer     NULL,
                ""Notes""              text        NULL,
                ""CreatedAt""          timestamptz NOT NULL DEFAULT now(),
                ""UpdatedAt""          timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ""IX_VehicleStatusHistories_VehicleId"" ON ""VehicleStatusHistories""(""VehicleId"");
            CREATE INDEX IF NOT EXISTS ""IX_VehicleStatusHistories_ChangedByStaffId"" ON ""VehicleStatusHistories""(""ChangedByStaffId"");

            -- Add TargetRole to Alerts if missing
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Alerts' AND column_name = 'TargetRole'
                ) THEN
                    ALTER TABLE ""Alerts"" ADD COLUMN ""TargetRole"" integer NULL;
                END IF;
            END $$;

            -- Add IsRead to Alerts if missing
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Alerts' AND column_name = 'IsRead'
                ) THEN
                    ALTER TABLE ""Alerts"" ADD COLUMN ""IsRead"" boolean NOT NULL DEFAULT false;
                END IF;
            END $$;

            -- Mark the AddGuestStatusFlow migration as applied
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260501120000_AddGuestStatusFlow', '9.0.0')
            ON CONFLICT DO NOTHING;

            -- AppConfig singleton table
            CREATE TABLE IF NOT EXISTS ""AppConfigs"" (
                ""Id""                   integer     NOT NULL PRIMARY KEY,
                ""EventTitle""           text        NOT NULL DEFAULT 'IsDB Annual Meetings 2025',
                ""MinimumLeadTimeHours"" integer     NOT NULL DEFAULT 2,
                ""UpdatedAt""            timestamptz NOT NULL DEFAULT now()
            );
            INSERT INTO ""AppConfigs"" (""Id"", ""EventTitle"", ""MinimumLeadTimeHours"", ""UpdatedAt"")
            VALUES (1, 'IsDB Annual Meetings 2025', 2, now())
            ON CONFLICT DO NOTHING;

            -- DepartureRequests schema update: add new columns if missing (each checked individually)
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'ManageToken'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ADD COLUMN ""ManageToken"" uuid NOT NULL DEFAULT gen_random_uuid();
                END IF;
            END $$;
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'HotelOptionId'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ADD COLUMN ""HotelOptionId"" uuid NULL REFERENCES ""HotelOptions""(""Id"") ON DELETE RESTRICT;
                END IF;
            END $$;
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'PickupDayOptionId'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ADD COLUMN ""PickupDayOptionId"" uuid NULL REFERENCES ""PickupDayOptions""(""Id"") ON DELETE RESTRICT;
                END IF;
            END $$;
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'PickupHourOptionId'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ADD COLUMN ""PickupHourOptionId"" uuid NULL REFERENCES ""PickupHourOptions""(""Id"") ON DELETE RESTRICT;
                END IF;
            END $$;
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'DisclaimerAccepted'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ADD COLUMN ""DisclaimerAccepted"" boolean NOT NULL DEFAULT false;
                END IF;
            END $$;
            DO $$ BEGIN
                -- If UpdatedAt column doesn't exist at all, add it
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'UpdatedAt'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ADD COLUMN ""UpdatedAt"" timestamptz NOT NULL DEFAULT now();
                -- If it exists but as text (from old SQLite migration), convert it
                ELSIF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'UpdatedAt'
                    AND data_type = 'text'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ALTER COLUMN ""UpdatedAt"" TYPE timestamptz USING ""UpdatedAt""::timestamptz;
                    ALTER TABLE ""DepartureRequests"" ALTER COLUMN ""UpdatedAt"" SET DEFAULT now();
                END IF;
            END $$;
            -- Convert other TEXT datetime columns in DepartureRequests to proper types if needed
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'CreatedAt'
                    AND data_type = 'text'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ALTER COLUMN ""CreatedAt"" TYPE timestamptz USING ""CreatedAt""::timestamptz;
                    ALTER TABLE ""DepartureRequests"" ALTER COLUMN ""CreatedAt"" SET DEFAULT now();
                END IF;
            END $$;
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'RequestedPickupTime'
                    AND data_type = 'text'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ALTER COLUMN ""RequestedPickupTime"" TYPE timestamptz USING ""RequestedPickupTime""::timestamptz;
                END IF;
            END $$;
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'ProcessedAt'
                    AND data_type = 'text'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ALTER COLUMN ""ProcessedAt"" TYPE timestamptz USING NULLIF(""ProcessedAt"", '')::timestamptz;
                END IF;
            END $$;
            -- Also convert Id, GuestId, ProcessedByStaffId from TEXT to UUID if needed
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'Id'
                    AND data_type = 'text'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ALTER COLUMN ""Id"" TYPE uuid USING ""Id""::uuid;
                    ALTER TABLE ""DepartureRequests"" ALTER COLUMN ""GuestId"" TYPE uuid USING ""GuestId""::uuid;
                    ALTER TABLE ""DepartureRequests"" ALTER COLUMN ""ProcessedByStaffId"" TYPE uuid USING ""ProcessedByStaffId""::uuid;
                END IF;
            END $$;
            -- Rename GuestName → FullName if old column exists
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'GuestName'
                ) AND NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'FullName'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" RENAME COLUMN ""GuestName"" TO ""FullName"";
                END IF;
            END $$;
            -- Rename GuestEmail → Email if old column exists
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'GuestEmail'
                ) AND NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'Email'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" RENAME COLUMN ""GuestEmail"" TO ""Email"";
                END IF;
            END $$;
            -- Add Email column if neither GuestEmail nor Email exist
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'Email'
                ) AND NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'GuestEmail'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ADD COLUMN ""Email"" text NOT NULL DEFAULT '';
                END IF;
            END $$;
            -- Add FullName column if neither GuestName nor FullName exist
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'FullName'
                ) AND NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'GuestName'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" ADD COLUMN ""FullName"" text NOT NULL DEFAULT '';
                END IF;
            END $$;
            -- Add SubmittedAt column (maps to CreatedAt if it exists, otherwise add fresh)
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'SubmittedAt'
                ) THEN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = 'DepartureRequests' AND column_name = 'CreatedAt'
                    ) THEN
                        -- Copy CreatedAt values to new SubmittedAt column
                        ALTER TABLE ""DepartureRequests"" ADD COLUMN ""SubmittedAt"" timestamptz NOT NULL DEFAULT now();
                        UPDATE ""DepartureRequests"" SET ""SubmittedAt"" = ""CreatedAt"";
                    ELSE
                        ALTER TABLE ""DepartureRequests"" ADD COLUMN ""SubmittedAt"" timestamptz NOT NULL DEFAULT now();
                    END IF;
                END IF;
            END $$;
            -- Drop old columns that are no longer used by the entity
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'DepartureRequests' AND column_name = 'GuestPhone'
                ) THEN
                    ALTER TABLE ""DepartureRequests"" DROP COLUMN IF EXISTS ""GuestPhone"";
                    ALTER TABLE ""DepartureRequests"" DROP COLUMN IF EXISTS ""RequestedPickupTime"";
                    ALTER TABLE ""DepartureRequests"" DROP COLUMN IF EXISTS ""DestinationAirport"";
                    ALTER TABLE ""DepartureRequests"" DROP COLUMN IF EXISTS ""FlightNumber"";
                    ALTER TABLE ""DepartureRequests"" DROP COLUMN IF EXISTS ""SpecialRequirements"";
                    ALTER TABLE ""DepartureRequests"" DROP COLUMN IF EXISTS ""IsProcessed"";
                    ALTER TABLE ""DepartureRequests"" DROP COLUMN IF EXISTS ""ProcessedAt"";
                    ALTER TABLE ""DepartureRequests"" DROP COLUMN IF EXISTS ""ProcessedByStaffId"";
                    ALTER TABLE ""DepartureRequests"" DROP COLUMN IF EXISTS ""GuestId"";
                END IF;
            END $$;
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_DepartureRequests_ManageToken"" ON ""DepartureRequests""(""ManageToken"");

            -- Mark the AddDepartureShuttle migration as applied
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260502000000_AddDepartureShuttle', '9.0.0')
            ON CONFLICT DO NOTHING;

            -- Mark the AddNotifications migration as applied (tables are pre-created above with correct PostgreSQL types)
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260501212329_AddNotifications', '9.0.0')
            ON CONFLICT DO NOTHING;

            -- NotificationTemplates table — drop and recreate with correct PostgreSQL types
            -- (EF Core migration used SQLite TEXT types for uuid/timestamptz columns which fail on PostgreSQL)
            DROP TABLE IF EXISTS ""NotificationTemplates"" CASCADE;
            CREATE TABLE ""NotificationTemplates"" (
                ""Id""              uuid        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                ""EventKey""        text        NOT NULL,
                ""EventLabel""      text        NOT NULL,
                ""MessageTemplate"" text        NOT NULL,
                ""TargetRoles""     text        NOT NULL DEFAULT 'All',
                ""Priority""        integer     NOT NULL DEFAULT 1,
                ""Description""     text        NOT NULL DEFAULT '',
                ""CreatedAt""       timestamptz NOT NULL DEFAULT now(),
                ""UpdatedAt""       timestamptz NOT NULL DEFAULT now()
            );
            CREATE UNIQUE INDEX ""IX_NotificationTemplates_EventKey"" ON ""NotificationTemplates""(""EventKey"");

            -- Mark the AddNotificationTemplates migration as applied
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260502071240_AddNotificationTemplates', '9.0.0')
            ON CONFLICT DO NOTHING;

            -- FlightDataLayerSeparation migration — pre-create TravelBookingHistories with correct PostgreSQL types
            -- (EF Core migration used SQLite TEXT types for uuid/timestamptz/boolean columns which fail on PostgreSQL)
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'TravelBookings' AND column_name = 'ChangedSinceLastView'
                ) THEN
                    ALTER TABLE ""TravelBookings"" ADD COLUMN ""ChangedSinceLastView"" boolean NOT NULL DEFAULT false;
                END IF;
            END $$;
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'TravelBookings' AND column_name = 'PreviousFlightNumber'
                ) THEN
                    ALTER TABLE ""TravelBookings"" ADD COLUMN ""PreviousFlightNumber"" text NULL;
                END IF;
            END $$;
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'TravelBookings' AND column_name = 'ChangedAt'
                ) THEN
                    ALTER TABLE ""TravelBookings"" ADD COLUMN ""ChangedAt"" timestamptz NULL;
                END IF;
            END $$;
            CREATE TABLE IF NOT EXISTS ""TravelBookingHistories"" (
                ""Id""                         uuid        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                ""TravelBookingId""             uuid        NOT NULL REFERENCES ""TravelBookings""(""Id"") ON DELETE CASCADE,
                ""GuestId""                     uuid        NOT NULL,
                ""PreviousFlightNumber""        text        NOT NULL,
                ""PreviousAirlineName""         text        NULL,
                ""PreviousScheduledArrival""    timestamptz NULL,
                ""PreviousScheduledDeparture""  timestamptz NULL,
                ""PreviousDeparturePort""       text        NULL,
                ""PreviousArrivalPort""         text        NULL,
                ""PreviousSeatClass""           text        NULL,
                ""ChangedAt""                   timestamptz NOT NULL DEFAULT now(),
                ""CreatedAt""                   timestamptz NOT NULL DEFAULT now(),
                ""UpdatedAt""                   timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ""IX_TravelBookingHistories_TravelBookingId"" ON ""TravelBookingHistories""(""TravelBookingId"");
            -- Mark the FlightDataLayerSeparation migration as applied
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260506042132_FlightDataLayerSeparation', '9.0.0')
            ON CONFLICT DO NOTHING;

        ");
        logger.LogInformation("CarClasses pre-creation complete.");

        // Add OAuthScope to EventsAirConfigs if missing (AddOAuthScopeToEventsAirConfig migration)
        await context.Database.ExecuteSqlRawAsync(@"
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE LOWER(table_name) = 'eventsairconfigs' AND LOWER(column_name) = 'oauthscope'
                ) THEN
                    ALTER TABLE ""EventsAirConfigs"" ADD COLUMN ""OAuthScope"" text NOT NULL DEFAULT '';
                END IF;
            END $$;
        ");

        // Make LicensePlate nullable in Vehicles (MakeVehicleLicensePlateOptional migration)
        await context.Database.ExecuteSqlRawAsync(@"
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Vehicles' AND column_name = 'LicensePlate'
                    AND is_nullable = 'NO'
                ) THEN
                    ALTER TABLE ""Vehicles"" ALTER COLUMN ""LicensePlate"" DROP NOT NULL;
                END IF;
            END $$;
            DROP INDEX IF EXISTS ""IX_Vehicles_LicensePlate"";
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Vehicles_LicensePlate""
                ON ""Vehicles""(""LicensePlate"")
                WHERE ""LicensePlate"" IS NOT NULL;
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260601100000_MakeVehicleLicensePlateOptional', '9.0.0')
            ON CONFLICT DO NOTHING;
        ");

        // Apply all remaining pending migrations
        logger.LogInformation("Applying pending migrations...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Migrations applied successfully.");

        } // end else (existing database)

        // Add EventCode columns to event-scoped entities — runs for ALL database types (idempotent)
        await context.Database.ExecuteSqlRawAsync(@"
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name='CarClasses' AND column_name='EventCode'
                ) THEN
                    ALTER TABLE ""CarClasses"" ADD COLUMN ""EventCode"" text NULL;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name='Vehicles' AND column_name='EventCode'
                ) THEN
                    ALTER TABLE ""Vehicles"" ADD COLUMN ""EventCode"" text NULL;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name='Drivers' AND column_name='EventCode'
                ) THEN
                    ALTER TABLE ""Drivers"" ADD COLUMN ""EventCode"" text NULL;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name='CarClassRules' AND column_name='EventCode'
                ) THEN
                    ALTER TABLE ""CarClassRules"" ADD COLUMN ""EventCode"" text NULL;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name='EventsAirSyncLogs' AND column_name='EventCode'
                ) THEN
                    ALTER TABLE ""EventsAirSyncLogs"" ADD COLUMN ""EventCode"" text NULL;
                END IF;
            END $$;
        ");

        // Notifications tables — Postgres path (safe to run on every startup, idempotent)
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Notifications"" (
                ""Id""               uuid        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                ""Message""          text        NOT NULL,
                ""TargetRoles""      text        NOT NULL DEFAULT 'All',
                ""Priority""         integer     NOT NULL DEFAULT 1,
                ""CreatedByStaffId"" uuid        NULL REFERENCES ""StaffUsers""(""Id"") ON DELETE SET NULL,
                ""CreatedAt""        timestamptz NOT NULL DEFAULT now(),
                ""UpdatedAt""        timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ""IX_Notifications_CreatedAt"" ON ""Notifications""(""CreatedAt"" DESC);
            CREATE INDEX IF NOT EXISTS ""IX_Notifications_CreatedByStaffId"" ON ""Notifications""(""CreatedByStaffId"");

            -- NotificationReads: EF Core model uses composite PK (NotificationId, StaffUserId).
            -- Drop and recreate if the table has the wrong schema (with Id column).
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'NotificationReads' AND column_name = 'Id'
                ) THEN
                    DROP TABLE IF EXISTS ""NotificationReads"";
                END IF;
            END $$;
            CREATE TABLE IF NOT EXISTS ""NotificationReads"" (
                ""NotificationId""   uuid        NOT NULL REFERENCES ""Notifications""(""Id"") ON DELETE CASCADE,
                ""StaffUserId""      uuid        NOT NULL REFERENCES ""StaffUsers""(""Id"") ON DELETE CASCADE,
                ""ReadAt""           timestamptz NOT NULL DEFAULT now(),
                PRIMARY KEY (""NotificationId"", ""StaffUserId"")
            );
            CREATE INDEX IF NOT EXISTS ""IX_NotificationReads_StaffUserId"" ON ""NotificationReads""(""StaffUserId"");
        ");

        // VehicleStatusHistories table — safe to run on every startup, idempotent
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""VehicleStatusHistories"" (
                ""Id""                 uuid        NOT NULL DEFAULT gen_random_uuid() PRIMARY KEY,
                ""VehicleId""          uuid        NOT NULL REFERENCES ""Vehicles""(""Id"") ON DELETE CASCADE,
                ""OldStatus""          integer     NOT NULL,
                ""NewStatus""          integer     NOT NULL,
                ""ChangedByStaffId""   uuid        NULL REFERENCES ""StaffUsers""(""Id"") ON DELETE SET NULL,
                ""ChangedByName""      text        NULL,
                ""ChangedByRole""      integer     NULL,
                ""Notes""              text        NULL,
                ""CreatedAt""          timestamptz NOT NULL DEFAULT now(),
                ""UpdatedAt""          timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ""IX_VehicleStatusHistories_VehicleId"" ON ""VehicleStatusHistories""(""VehicleId"");
            CREATE INDEX IF NOT EXISTS ""IX_VehicleStatusHistories_ChangedByStaffId"" ON ""VehicleStatusHistories""(""ChangedByStaffId"");
        ");
    }
    else
    {
        // SQLite local dev — EnsureCreated creates all tables for a brand-new DB.
        logger.LogInformation("SQLite mode (local development). Running EnsureCreated...");
        await context.Database.EnsureCreatedAsync();

        // EnsureCreated only creates tables if the DB is brand new.
        // For existing DBs, manually create new tables that were added after initial creation.
        // Notifications table
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS [Notifications] (" +
            "[Id] TEXT NOT NULL PRIMARY KEY, " +
            "[Message] TEXT NOT NULL, " +
            "[TargetRoles] TEXT NOT NULL DEFAULT 'All', " +
            "[Priority] INTEGER NOT NULL DEFAULT 1, " +
            "[CreatedByStaffId] TEXT NULL, " +
            "[CreatedAt] TEXT NOT NULL DEFAULT (datetime('now')), " +
            "[UpdatedAt] TEXT NOT NULL DEFAULT (datetime('now'))" +
            ");"
        );
        // NotificationReads: EF Core uses composite PK (NotificationId, StaffUserId).
        // If the table was created by EnsureCreated with an incorrect schema (Id column),
        // we need to drop and recreate it. Check if the Id column exists.
        var hasIdColumn = await context.Database.ExecuteSqlRawAsync(
            "SELECT CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END FROM pragma_table_info('NotificationReads') WHERE name='Id'"
        );
        // Drop table if it has the wrong schema (with Id column) so EF Core can use composite PK
        await context.Database.ExecuteSqlRawAsync(
            "DROP TABLE IF EXISTS [NotificationReads]"
        );
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS [NotificationReads] (" +
            "[NotificationId] TEXT NOT NULL, " +
            "[StaffUserId] TEXT NOT NULL, " +
            "[ReadAt] TEXT NOT NULL DEFAULT (datetime('now')), " +
            "PRIMARY KEY ([NotificationId], [StaffUserId]), " +
            "FOREIGN KEY ([NotificationId]) REFERENCES [Notifications]([Id]) ON DELETE CASCADE, " +
            "FOREIGN KEY ([StaffUserId]) REFERENCES [StaffUsers]([Id]) ON DELETE CASCADE" +
            ");"
        );
        // NotificationTemplates table (SQLite)
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS [NotificationTemplates] (" +
            "[Id] TEXT NOT NULL PRIMARY KEY, " +
            "[EventKey] TEXT NOT NULL, " +
            "[EventLabel] TEXT NOT NULL, " +
            "[MessageTemplate] TEXT NOT NULL, " +
            "[TargetRoles] TEXT NOT NULL DEFAULT 'All', " +
            "[Priority] INTEGER NOT NULL DEFAULT 1, " +
            "[Description] TEXT NOT NULL DEFAULT '', " +
            "[CreatedAt] TEXT NOT NULL DEFAULT (datetime('now')), " +
            "[UpdatedAt] TEXT NOT NULL DEFAULT (datetime('now'))" +
            ");"
        );
        await context.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS [IX_NotificationTemplates_EventKey] ON [NotificationTemplates]([EventKey]);"
        );

    }

    await DatabaseSeeder.SeedAsync(context, logger);

    // Seed notification templates (idempotent — only inserts missing keys)
    var templateService = scope.ServiceProvider.GetRequiredService<IsDB.Hospitality.API.Services.NotificationTemplateService>();
    await templateService.SeedDefaultsAsync();
    logger.LogInformation("Notification templates seeded.");
}

// Swagger enabled in all environments for demo/testing
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "IsDB Hospitality API v1"));

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");

// Serve React frontend static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// SPA fallback — all non-API routes return index.html for React Router
app.MapFallbackToFile("index.html");

app.Run();

public class SlugifyParameterTransformer : Microsoft.AspNetCore.Routing.IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value)
        => value?.ToString()?.ToLowerInvariant();
}
