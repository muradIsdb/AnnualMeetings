using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using MediatR;

namespace IsDB.Hospitality.Application.Features.DepartureRequests.Commands;

public record CreateDepartureRequestCommand(
    string GuestName,
    string? GuestEmail,
    string? GuestPhone,
    string? HotelName,
    string? RoomNumber,
    DateTime RequestedPickupTime,
    string DestinationAirport,
    string? FlightNumber,
    string? SpecialRequirements) : IRequest<Guid>;

public class CreateDepartureRequestCommandHandler : IRequestHandler<CreateDepartureRequestCommand, Guid>
{
    private readonly IAppDbContext _context;

    public CreateDepartureRequestCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateDepartureRequestCommand request, CancellationToken cancellationToken)
    {
        var departureRequest = new DepartureRequest
        {
            GuestName = request.GuestName,
            GuestEmail = request.GuestEmail,
            GuestPhone = request.GuestPhone,
            HotelName = request.HotelName,
            RoomNumber = request.RoomNumber,
            RequestedPickupTime = request.RequestedPickupTime,
            DestinationAirport = request.DestinationAirport,
            FlightNumber = request.FlightNumber,
            SpecialRequirements = request.SpecialRequirements
        };

        _context.DepartureRequests.Add(departureRequest);
        await _context.SaveChangesAsync(cancellationToken);
        return departureRequest.Id;
    }
}
