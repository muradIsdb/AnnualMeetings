namespace IsDB.Hospitality.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendDepartureConfirmationAsync(
        string toEmail,
        string toName,
        string hotelName,
        string roomNumber,
        string pickupDay,
        string pickupHour,
        string manageUrl,
        CancellationToken cancellationToken = default);

    Task SendDepartureUpdateAsync(
        string toEmail,
        string toName,
        string hotelName,
        string roomNumber,
        string pickupDay,
        string pickupHour,
        string manageUrl,
        CancellationToken cancellationToken = default);

    Task SendDepartureCancellationAsync(
        string toEmail,
        string toName,
        string reRegisterUrl,
        CancellationToken cancellationToken = default);
}
