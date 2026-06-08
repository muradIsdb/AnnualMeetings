using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Dashboard;
using IsDB.Hospitality.Application.DTOs.Guests;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Guests.Queries;

public record GetGuestsQuery(GuestStatus? Status = null, bool? IsCritical = null, string? ActiveEventCode = null) : IRequest<List<GuestSummaryDto>>;

public class GetGuestsQueryHandler : IRequestHandler<GetGuestsQuery, List<GuestSummaryDto>>
{
    private readonly IAppDbContext _context;

    public GetGuestsQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<List<GuestSummaryDto>> Handle(GetGuestsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Guests.Where(g => g.IsActive).AsQueryable();

        // Filter by active event code: show guests with no event code (legacy) or matching the active event
        if (!string.IsNullOrWhiteSpace(request.ActiveEventCode))
            query = query.Where(g => g.EventCode == null || g.EventCode == request.ActiveEventCode);

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
                Country = g.Country,
                PhotoUrl = g.PhotoUrl,
                IsCritical = g.IsCritical,
                RequiresAccessibility = g.RequiresAccessibility,
                StatusLabel = g.Status.ToString(),
                Notes = g.Notes,
                RegistrationTypeName = g.RegistrationTypeName,
                RankValue = g.RankValue,
                VehicleTypeValue = g.VehicleTypeValue,
                DedicatedCar = g.DedicatedCar,
                LiaisonOfficer = g.LiaisonOfficer,
                InvitedToOpeningCeremony = g.InvitedToOpeningCeremony,
                OldHotel = g.OldHotel,
                LiaisonOfficerName = g.LiaisonOfficerName,
                LiaisonOfficerCarNumber = g.LiaisonOfficerCarNumber,
                DeservedCarClassId = g.DeservedCarClassId,
                DeservedCarClassName = g.DeservedCarClass != null ? g.DeservedCarClass.Name : null,
                DeservedCarClassColor = g.DeservedCarClass != null ? g.DeservedCarClass.Color : null,
                ArrivalTime = g.TravelBookings
                    .Where(tb => tb.IsArrival)
                    .Select(tb => (DateTime?)tb.Flight.ScheduledArrival)
                    .FirstOrDefault(),
                InboundStatus = g.InboundStatus,
                InboundStatusLabel = g.InboundStatus.ToString(),
                HasChangedFlights = g.TravelBookings.Any(tb => tb.ChangedSinceLastView),
                OutboundStatus = g.OutboundStatus,
                OutboundStatusLabel = g.OutboundStatus.HasValue ? g.OutboundStatus.Value.ToString() : null,
                HotelName = g.HotelName,
                RoomNumber = g.RoomNumber,
                Organization = g.Organization
            })
            .ToListAsync(cancellationToken);
    }
}
