using xstackApi.Models;

namespace xstackApi.Services;

public interface IAuthService
{
    Task<AuthTokenResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthTokenResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthUserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
}
