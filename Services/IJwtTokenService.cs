using xstackApi.Models;

namespace xstackApi.Services;

public interface IJwtTokenService
{
    AuthTokenResponse CreateToken(AuthUser user);
}
