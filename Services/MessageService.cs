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
        DateTimeOffset? myReadAt;
        await using (var memberCmd = dataSource.CreateCommand(
            """
            SELECT m0.created_at
            FROM chat_members cm
            LEFT JOIN messages m0 ON m0.id = cm.last_read_message_id
            WHERE cm.chat_id = @chatId AND cm.user_id = @currentUserId;
            """))
        {
            memberCmd.Parameters.AddWithValue("chatId", chatId);
            memberCmd.Parameters.AddWithValue("currentUserId", currentUserId);

            await using var memberReader = await memberCmd.ExecuteReaderAsync(cancellationToken);
            if (!await memberReader.ReadAsync(cancellationToken))
            {
                return ServiceResult<List<MessageResponse>>.NotFound("Chat not found or access denied.");
            }

            myReadAt = memberReader.IsDBNull(0)
                ? null
                : memberReader.GetFieldValue<DateTimeOffset>(0);
        }

        // Earliest cursor among the other members. A NULL cursor means that
        // member has read nothing yet, so the whole chat counts as unread by
        // them and the current user's own messages stay unread.
        DateTimeOffset? othersMinReadAt;
        await using (var othersCmd = dataSource.CreateCommand(
            """
            SELECT CASE
                       WHEN COUNT(*) FILTER (WHERE m0.created_at IS NULL) > 0 THEN NULL
                       ELSE MIN(m0.created_at)
                   END
            FROM chat_members cm
            LEFT JOIN messages m0 ON m0.id = cm.last_read_message_id
            WHERE cm.chat_id = @chatId AND cm.user_id <> @currentUserId;
            """))
        {
            othersCmd.Parameters.AddWithValue("chatId", chatId);
            othersCmd.Parameters.AddWithValue("currentUserId", currentUserId);

            await using var othersReader = await othersCmd.ExecuteReaderAsync(cancellationToken);
            await othersReader.ReadAsync(cancellationToken);
            othersMinReadAt = othersReader.IsDBNull(0)
                ? null
                : othersReader.GetFieldValue<DateTimeOffset>(0);
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
            var isEdited = reader.GetBoolean(7);
            var isDeleted = reader.GetBoolean(8);
            var createdAt = ToDateTimeOffset(reader.GetValue(9));
            var updatedAt = ToDateTimeOffset(reader.GetValue(10));

            var senderName = displayName ?? username ?? phoneNumber ?? "User";
            var displayContent = isDeleted ? null : content;
            var isMine = senderId == currentUserId;
            var isRead = ReadState.IsRead(
                isMine,
                createdAt,
                myReadAt,
                othersMinReadAt);

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
