using xstackApi.Models;

namespace xstackApi.Services;

public interface IChatService
{
    Task<ServiceResult<List<ChatSummaryResponse>>> GetUserChatsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<ServiceResult<ChatDetailsResponse>> GetChatDetailsAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(
        Guid chatId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<ServiceResult<ChatDetailsResponse>> CreateDirectChatAsync(
        Guid currentUserId,
        CreateDirectChatRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ChatDetailsResponse>> CreateGroupChatAsync(
        Guid currentUserId,
        CreateGroupChatRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ChatDetailsResponse>> UpdateChatAsync(
        Guid chatId,
        Guid currentUserId,
        UpdateChatRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> DeleteChatAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<ServiceResult<List<ChatMemberResponse>>> GetChatMembersAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<ServiceResult<ChatMemberResponse>> AddMemberAsync(
        Guid chatId,
        Guid currentUserId,
        AddChatMemberRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> RemoveMemberAsync(
        Guid chatId,
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken);

    Task<ServiceResult> UpdateMemberRoleAsync(
        Guid chatId,
        Guid currentUserId,
        Guid targetUserId,
        UpdateMemberRoleRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> LeaveChatAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken);
}
