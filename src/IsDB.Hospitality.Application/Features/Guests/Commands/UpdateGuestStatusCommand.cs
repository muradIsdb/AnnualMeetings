using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Guests.Commands;

public record UpdateGuestStatusCommand(Guid GuestId, GuestStatus NewStatus, string? Notes = null) : IRequest<bool>;

public class UpdateGuestStatusCommandHandler : IRequestHandler<UpdateGuestStatusCommand, bool>
{
    private readonly IAppDbContext _context;

    public UpdateGuestStatusCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateGuestStatusCommand request, CancellationToken cancellationToken)
    {
        var guest = await _context.Guests.FindAsync(new object[] { request.GuestId }, cancellationToken);
        if (guest == null) return false;

        guest.Status = request.NewStatus;
        if (request.Notes != null) guest.Notes = request.Notes;
        guest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
