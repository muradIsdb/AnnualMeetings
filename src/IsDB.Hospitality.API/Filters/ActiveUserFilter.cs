using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace IsDB.Hospitality.API.Filters;

/// <summary>
/// Global action filter that rejects requests from deactivated users immediately,
/// even if their JWT token is still cryptographically valid.
/// Uses a short-lived in-memory cache (30 s TTL) to avoid a DB hit on every request.
/// </summary>
public class ActiveUserFilter : IAsyncActionFilter
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public ActiveUserFilter(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Only apply to authenticated requests
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            await next();
            return;
        }

        var cacheKey = $"user_active_{userId}";

        // Check cache first (30-second TTL)
        if (!_cache.TryGetValue(cacheKey, out bool isActive))
        {
            isActive = await _db.StaffUsers
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.IsActive)
                .FirstOrDefaultAsync();

            _cache.Set(cacheKey, isActive, TimeSpan.FromSeconds(30));
        }

        if (!isActive)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Account has been deactivated." });
            return;
        }

        await next();
    }
}
