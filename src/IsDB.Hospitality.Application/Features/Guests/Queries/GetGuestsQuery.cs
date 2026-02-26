using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Application.DTOs.Guests;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Guests.Queries;

public record GetGuestsQuery(GuestStatus? Status = null, bool? IsCritical = null) : IRequest<List<GuestSummaryDto>>;

public class GetGuestsQueryHandler : IRequestHandler<GetGuestsQuery, List<GuestSummaryDto>>
{
    private readonly IAppDbContext _context;

    public GetGuestsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<List<GuestSummaryDto>> Handle(GetGuestsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Guests.AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(g => g.Status == request.Status.Value);

        if (request.IsCritical.HasValue)
            query = query.Where(g => g.IsCritical == request.IsCritical.Value);

        return await query
            .OrderByDescending(g => g.IsCritical)
            .ThenBy(g => g.LastName)
            .Select(g => new GuestSummaryDto
            {
                Id = g.Id,
                FullName = g.FirstName + " " + g.LastName,
                Designation = g.Designation,
                Nationality = g.Nationality,
                PhotoUrl = g.PhotoUrl,
                IsCritical = g.IsCritical,
                RequiresAccessibility = g.RequiresAccessibility,
                StatusLabel = g.Status.ToString(),
                Notes = g.Notes,
                RegistrationTypeName = g.RegistrationTypeName
            })
            .ToListAsync(cancellationToken);
    }
}
