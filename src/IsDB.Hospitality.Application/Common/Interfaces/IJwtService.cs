using IsDB.Hospitality.Domain.Entities;

namespace IsDB.Hospitality.Application.Common.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(StaffUser user);
    string GenerateRefreshToken();
    bool ValidateRefreshToken(string token);
}
