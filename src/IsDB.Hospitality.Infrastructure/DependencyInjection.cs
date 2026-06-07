using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Infrastructure.BackgroundServices;
using IsDB.Hospitality.Infrastructure.ExternalClients.EventsAir;
using IsDB.Hospitality.Infrastructure.ExternalClients.FlightTracker;
using IsDB.Hospitality.Infrastructure.Persistence;
using IsDB.Hospitality.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IsDB.Hospitality.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database — PostgreSQL if DATABASE_URL is set (Railway), otherwise SQLite for local dev
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

        services.AddDbContext<AppDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(databaseUrl))
            {
                // Railway provides DATABASE_URL in the format:
                // postgresql://user:password@host:port/dbname
                var connectionString = ConvertDatabaseUrlToNpgsql(databaseUrl);
                options.UseNpgsql(connectionString, b =>
                    b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            }
            else
            {
                // Local development — SQLite
                options.UseSqlite(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
            }
        });

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // Memory cache for token caching
        services.AddMemoryCache();

        // External clients
        services.Configure<EventsAirOptions>(configuration.GetSection("EventsAir"));
        services.Configure<AviationstackOptions>(configuration.GetSection("Aviationstack"));

        services.AddHttpClient<IEventsAirClient, EventsAirClient>();
        services.AddHttpClient<IFlightTrackerClient, AviationstackClient>();

        // Services
        services.AddScoped<IJwtService, JwtService>();
        services.AddSingleton<ISystemLogService, SystemLogService>();

        // Background services
        services.AddHostedService<EventsAirSyncService>();
        services.AddHostedService<FlightTrackerSyncService>();
        services.AddHostedService<LogRetentionService>();

        return services;
    }

    /// <summary>
    /// Converts a DATABASE_URL (postgres:// or postgresql://) to an Npgsql connection string.
    /// Example input:  postgresql://user:password@host:5432/dbname
    /// Example output: Host=host;Port=5432;Database=dbname;Username=user;Password=password;SSL Mode=Require;Trust Server Certificate=true
    /// </summary>
    private static string ConvertDatabaseUrlToNpgsql(string databaseUrl)
    {
        // Railway may use postgres:// or postgresql:// prefix
        var uri = new Uri(databaseUrl.Replace("postgres://", "postgresql://"));

        var userInfo = uri.UserInfo.Split(':');
        var username = userInfo[0];
        var password = userInfo.Length > 1 ? userInfo[1] : "";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
    }
}
