using xstackApi.Models;

namespace xstackApi.Services;

public interface IUserService
{
    Task<ServiceResult<List<UserSearchResultResponse>>> SearchUsersAsync(
        string query,
        Guid currentUserId,
        int limit,
        CancellationToken cancellationToken);

    Task<ServiceResult<UserProfileResponse>> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
