using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Application.DTOs.Vehicles;
using IsDB.Hospitality.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IsDB.Hospitality.Application.Features.Vehicles.Queries;

public record GetAvailableVehiclesQuery : IRequest<List<VehicleDto>>;

public class GetAvailableVehiclesQueryHandler : IRequestHandler<GetAvailableVehiclesQuery, List<VehicleDto>>
{
    private readonly IAppDbContext _context;

    public GetAvailableVehiclesQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<List<VehicleDto>> Handle(GetAvailableVehiclesQuery request, CancellationToken cancellationToken)
    {
        return await _context.Vehicles
            .Where(v => v.Status == VehicleStatus.Available)
            .Select(v => new VehicleDto
            {
                Id = v.Id,
                LicensePlate = v.LicensePlate,
                Make = v.Make,
                Model = v.Model,
                Color = v.Color,
                DriverName = v.DriverName,
                DriverPhone = v.DriverPhone,
                Status = v.Status,
                BarcodeValue = v.BarcodeValue
            })
            .ToListAsync(cancellationToken);
    }
}
