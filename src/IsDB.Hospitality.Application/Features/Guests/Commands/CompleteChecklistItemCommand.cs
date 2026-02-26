using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using MediatR;

namespace IsDB.Hospitality.Application.Features.Guests.Commands;

public record CompleteChecklistItemCommand(
    Guid GuestId,
    Guid ChecklistItemId,
    Guid StaffUserId,
    string? Notes = null) : IRequest<bool>;

public class CompleteChecklistItemCommandHandler : IRequestHandler<CompleteChecklistItemCommand, bool>
{
    private readonly IAppDbContext _context;

    public CompleteChecklistItemCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(CompleteChecklistItemCommand request, CancellationToken cancellationToken)
    {
        // Check if already completed
        var existing = _context.ChecklistCompletions
            .FirstOrDefault(cc => cc.GuestId == request.GuestId && cc.ChecklistItemId == request.ChecklistItemId);

        if (existing != null) return true; // Already done

        var completion = new ChecklistCompletion
        {
            GuestId = request.GuestId,
            ChecklistItemId = request.ChecklistItemId,
            CompletedByStaffId = request.StaffUserId,
            Notes = request.Notes,
            CompletedAt = DateTime.UtcNow
        };

        _context.ChecklistCompletions.Add(completion);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
