using IsDB.Hospitality.Application.Common.Interfaces;
using IsDB.Hospitality.Domain.Entities;
using MediatR;

namespace IsDB.Hospitality.Application.Features.DepartureRequests.Commands;

/// <summary>
/// Legacy MediatR command — kept for backward compatibility.
/// New submissions are handled directly in DepartureRequestsController.
/// </summary>
public record CreateDepartureRequestCommand(
    string FullName,
    string Email,
    string RoomNumber,
    Guid HotelOptionId,
    Guid PickupDayOptionId,
    Guid PickupHourOptionId) : IRequest<Guid>;

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
            FullName = request.FullName,
            Email = request.Email,
            RoomNumber = request.RoomNumber,
            HotelOptionId = request.HotelOptionId,
            PickupDayOptionId = request.PickupDayOptionId,
            PickupHourOptionId = request.PickupHourOptionId,
            DisclaimerAccepted = true,
            ManageToken = Guid.NewGuid(),
            SubmittedAt = DateTime.UtcNow,
        };

        _context.DepartureRequests.Add(departureRequest);
        await _context.SaveChangesAsync(cancellationToken);
        return departureRequest.Id;
    }
}
