using xstackApi.Models;

namespace xstackApi.Services;

public interface IJwtTokenService
{
    AccessToken CreateToken(AuthUser user);
}
