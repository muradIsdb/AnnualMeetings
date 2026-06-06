using System.Text;
using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.SystemLogs;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using IsDB.Hospitality.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.API.Controllers;

[ApiController]
[Route("api/system-logs")]
[Authorize(Roles = "Admin")]
public class SystemLogsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ISystemLogService _logService;

    public SystemLogsController(IAppDbContext db, ISystemLogService logService)
    {
        _db = db;
        _logService = logService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? severity = null,
        [FromQuery] string? module = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;

        var query = BuildQuery(severity, module, search, startDate, endDate);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var logs = await query
            .OrderByDescending(l => l.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new SystemLogDto
            {
                Id = l.Id,
                OccurredAt = l.OccurredAt,
                Severity = l.Severity,
                Module = l.Module,
                Title = l.Title,
                Detail = l.Detail,
                RequestPath = l.RequestPath,
                StaffName = l.StaffName,
                CorrelationId = l.CorrelationId
            })
            .ToListAsync();

        return Ok(new
        {
            items = logs,
            totalCount,
            totalPages,
            page,
            pageSize
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportLogs(
        [FromQuery] string? severity = null,
        [FromQuery] string? module = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var query = BuildQuery(severity, module, search, startDate, endDate);

        var logs = await query
            .OrderByDescending(l => l.OccurredAt)
            .Select(l => new SystemLogDto
            {
                OccurredAt = l.OccurredAt,
                Severity = l.Severity,
                Module = l.Module,
                Title = l.Title,
                Detail = l.Detail,
                RequestPath = l.RequestPath,
                StaffName = l.StaffName,
                CorrelationId = l.CorrelationId
            })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("OccurredAt,Severity,Module,Title,StaffName,CorrelationId,RequestPath,Detail");

        foreach (var log in logs)
        {
            sb.AppendLine($"{log.OccurredAt:yyyy-MM-dd HH:mm:ss},{log.Severity},{EscapeCsv(log.Module)},{EscapeCsv(log.Title)},{EscapeCsv(log.StaffName)},{EscapeCsv(log.CorrelationId)},{EscapeCsv(log.RequestPath)},{EscapeCsv(log.Detail)}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"system-logs-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestLog()
    {
        try
        {
            await _logService.LogAsync(LogSeverity.Information, "Test", "Test log entry from API", "This is a test detail.");
            var count = await _db.SystemLogs.CountAsync();
            return Ok(new { message = "Test log written.", totalLogs = count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message, stack = ex.StackTrace });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> ClearLogs()
    {
        var count = await _db.SystemLogs.ExecuteDeleteAsync();

        var staffIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var staffName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        Guid? staffId = Guid.TryParse(staffIdStr, out var id) ? id : null;

        await _logService.LogAsync(
            LogSeverity.Information,
            "Admin",
            $"System logs cleared ({count} entries deleted)",
            null,
            HttpContext.Request.Path,
            staffId,
            staffName
        );

        return Ok(new { message = $"Cleared {count} logs." });
    }

    private IQueryable<SystemLog> BuildQuery(
        string? severityStr,
        string? module,
        string? search,
        DateTime? startDate,
        DateTime? endDate)
    {
        var query = _db.SystemLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(severityStr))
        {
            var severities = severityStr.Split(',')
                .Select(s => Enum.TryParse<LogSeverity>(s, true, out var parsed) ? (LogSeverity?)parsed : null)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .ToList();

            if (severities.Any())
            {
                query = query.Where(l => severities.Contains(l.Severity));
            }
        }

        if (!string.IsNullOrWhiteSpace(module) && module != "All")
        {
            query = query.Where(l => l.Module == module);
        }

        if (startDate.HasValue)
        {
            query = query.Where(l => l.OccurredAt >= startDate.Value.ToUniversalTime());
        }

        if (endDate.HasValue)
        {
            query = query.Where(l => l.OccurredAt <= endDate.Value.ToUniversalTime().AddDays(1));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(l =>
                l.Title.ToLower().Contains(s) ||
                (l.Detail != null && l.Detail.ToLower().Contains(s)));
        }

        return query;
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}
