using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Alerts;
using IsDB.Hospitality.Domain.Entities;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Alerts.Commands;

public record CreateAlertCommand(
    Guid? GuestId,
    string Title,
    string Message,
    AlertSeverity Severity) : IRequest<Guid>;

public class CreateAlertCommandHandler : IRequestHandler<CreateAlertCommand, Guid>
{
    private readonly IAppDbContext _context;

    public CreateAlertCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = new Alert
        {
            GuestId = request.GuestId,
            Title = request.Title,
            Message = request.Message,
            Severity = request.Severity
        };

        _context.Alerts.Add(alert);
        await _context.SaveChangesAsync(cancellationToken);
        return alert.Id;
    }
}

public record ResolveAlertCommand(Guid AlertId, Guid StaffUserId, string? Notes = null) : IRequest<bool>;

public class ResolveAlertCommandHandler : IRequestHandler<ResolveAlertCommand, bool>
{
    private readonly IAppDbContext _context;

    public ResolveAlertCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ResolveAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _context.Alerts.FindAsync(new object[] { request.AlertId }, cancellationToken);
        if (alert == null) return false;

        alert.IsResolved = true;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.ResolvedByStaffId = request.StaffUserId;
        alert.ResolutionNotes = request.Notes;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
