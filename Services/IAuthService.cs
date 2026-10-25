using xstackApi.Models;

namespace xstackApi.Services;

public interface IAuthService
{
    Task<AuthTokenResponse?> RegisterAsync(
        RegisterRequest request,
        string? deviceInfo,
        CancellationToken cancellationToken);

    Task<AuthTokenResponse?> LoginAsync(
        LoginRequest request,
        string? deviceInfo,
        CancellationToken cancellationToken);

    Task<AuthTokenResponse?> RefreshAsync(
        RefreshTokenRequest request,
        string? deviceInfo,
        CancellationToken cancellationToken);

    Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task<AuthUserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
}
