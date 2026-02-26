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
        // Database — SQLite for sandbox demo; swap to UseSqlServer for production
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

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

        // Background services
        services.AddHostedService<EventsAirSyncService>();
        services.AddHostedService<FlightTrackerSyncService>();

        return services;
    }
}
