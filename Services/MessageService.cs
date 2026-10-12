using Microsoft.AspNetCore.SignalR;
using Npgsql;
using xstackApi.Hubs;
using xstackApi.Models;

namespace xstackApi.Services;

public sealed partial class MessageService(
    NpgsqlDataSource dataSource,
    IHubContext<ChatHub, IChatClient> hubContext) : IMessageService
{
    public async Task<ServiceResult<List<MessageResponse>>> GetMessagesAsync(
        Guid chatId,
        Guid currentUserId,
        int limit,
        Guid? beforeMessageId,
        CancellationToken cancellationToken)
    {
        await using (var checkCmd = dataSource.CreateCommand(
            "SELECT 1 FROM chat_members WHERE chat_id = @chatId AND user_id = @currentUserId;"))
        {
            checkCmd.Parameters.AddWithValue("chatId", chatId);
            checkCmd.Parameters.AddWithValue("currentUserId", currentUserId);
            var isMember = await checkCmd.ExecuteScalarAsync(cancellationToken);
            if (isMember is null)
            {
                return ServiceResult<List<MessageResponse>>.NotFound("Chat not found or access denied.");
            }
        }

        DateTimeOffset? cursorTime = null;
        if (beforeMessageId.HasValue)
        {
            await using var cursorCmd = dataSource.CreateCommand(
                "SELECT created_at FROM messages WHERE id = @id AND chat_id = @chatId;");
            cursorCmd.Parameters.AddWithValue("id", beforeMessageId.Value);
            cursorCmd.Parameters.AddWithValue("chatId", chatId);
            var res = await cursorCmd.ExecuteScalarAsync(cancellationToken);
            if (res is DateTimeOffset dt)
            {
                cursorTime = dt;
            }
            else if (res is not null)
            {
                cursorTime = ToDateTimeOffset(res);
            }
        }

        var clampedLimit = Math.Clamp(limit, 1, 100);

        await using var command = dataSource.CreateCommand(
            """
            SELECT 
                m.id,
                m.chat_id,
                m.sender_id,
                u.display_name,
                u.username,
                u.phone_number,
                m.content,
                m.is_read,
                m.is_edited,
                m.is_deleted,
                m.created_at,
                m.updated_at
            FROM messages m
            LEFT JOIN users u ON u.id = m.sender_id
            WHERE m.chat_id = @chatId
              AND (@cursorTime::timestamptz IS NULL OR m.created_at < @cursorTime::timestamptz)
            ORDER BY m.created_at DESC
            LIMIT @limit;
            """);

        command.Parameters.AddWithValue("chatId", chatId);
        command.Parameters.AddWithValue("cursorTime", (object?)cursorTime ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", clampedLimit);

        var messages = new List<MessageResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetGuid(0);
            var cId = reader.GetGuid(1);
            var senderId = reader.IsDBNull(2) ? (Guid?)null : reader.GetGuid(2);
            var displayName = reader.IsDBNull(3) ? null : reader.GetString(3);
            var username = reader.IsDBNull(4) ? null : reader.GetString(4);
            var phoneNumber = reader.IsDBNull(5) ? null : reader.GetString(5);
            var content = reader.IsDBNull(6) ? null : reader.GetString(6);
            var isRead = reader.GetBoolean(7);
            var isEdited = reader.GetBoolean(8);
            var isDeleted = reader.GetBoolean(9);
            var createdAt = ToDateTimeOffset(reader.GetValue(10));
            var updatedAt = ToDateTimeOffset(reader.GetValue(11));

            var senderName = displayName ?? username ?? phoneNumber ?? "User";
            var displayContent = isDeleted ? null : content;
            var isMine = senderId == currentUserId;

            messages.Add(new MessageResponse(
                id,
                cId,
                senderId,
                senderName,
                username,
                displayContent,
                isRead,
                isEdited,
                isDeleted,
                createdAt,
                updatedAt,
                isMine));
        }

        messages.Reverse();

        return ServiceResult<List<MessageResponse>>.Success(messages);
    }

    private static DateTimeOffset ToDateTimeOffset(object? value) => value switch
    {
        DateTimeOffset dto => dto,
        DateTime dt => dt.Kind == DateTimeKind.Utc ? new DateTimeOffset(dt) : new DateTimeOffset(dt, TimeSpan.Zero),
        _ => DateTimeOffset.UtcNow
    };
}
