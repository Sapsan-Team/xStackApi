using xstackApi.Models;

namespace xstackApi.Services;

public interface IMessageService
{
    Task<ServiceResult<List<MessageResponse>>> GetMessagesAsync(
        Guid chatId,
        Guid currentUserId,
        int limit,
        Guid? beforeMessageId,
        CancellationToken cancellationToken);

    Task<ServiceResult<MessageResponse>> SendMessageAsync(
        Guid chatId,
        Guid currentUserId,
        SendMessageRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<MessageResponse>> EditMessageAsync(
        Guid chatId,
        Guid messageId,
        Guid currentUserId,
        EditMessageRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult> DeleteMessageAsync(
        Guid chatId,
        Guid messageId,
        Guid currentUserId,
        CancellationToken cancellationToken);

    Task<ServiceResult<MarkReadResponse>> MarkAsReadAsync(
        Guid chatId,
        Guid currentUserId,
        MarkMessagesReadRequest request,
        CancellationToken cancellationToken);
}
