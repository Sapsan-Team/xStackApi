using Npgsql;
using xstackApi.Hubs;
using xstackApi.Models;

namespace xstackApi.Services;

public sealed partial class ChatService(
    NpgsqlDataSource dataSource,
    IChatRoomService chatRoomService) : IChatService
{
    public async Task<ServiceResult<List<ChatSummaryResponse>>> GetUserChatsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT 
                c.id,
                c.type::text,
                c.title,
                c.avatar_url,
                c.created_at,
                c.updated_at,
                my_cm.role::text AS my_role,
                other_u.id AS other_user_id,
                other_u.display_name AS other_user_display_name,
                other_u.username AS other_user_username,
                other_u.phone_number AS other_user_phone,
                lm.id AS last_msg_id,
                lm.sender_id AS last_msg_sender_id,
                lm_u.display_name AS last_msg_sender_display_name,
                lm_u.username AS last_msg_sender_username,
                lm_u.phone_number AS last_msg_sender_phone,
                lm.content AS last_msg_content,
                lm.created_at AS last_msg_created_at,
                lm.is_read AS last_msg_is_read,
                lm.is_deleted AS last_msg_is_deleted,
                COALESCE(unread.count, 0) AS unread_count
            FROM chats c
            JOIN chat_members my_cm ON my_cm.chat_id = c.id AND my_cm.user_id = @currentUserId
            LEFT JOIN LATERAL (
                SELECT u.id, u.display_name, u.username, u.phone_number
                FROM chat_members cm2
                JOIN users u ON u.id = cm2.user_id
                WHERE cm2.chat_id = c.id AND cm2.user_id != @currentUserId AND c.type = 'private'::chat_type
                LIMIT 1
            ) other_u ON true
            LEFT JOIN LATERAL (
                SELECT m.id, m.sender_id, m.content, m.created_at, m.is_read, m.is_deleted
                FROM messages m
                WHERE m.chat_id = c.id
                ORDER BY m.created_at DESC
                LIMIT 1
            ) lm ON true
            LEFT JOIN users lm_u ON lm_u.id = lm.sender_id
            LEFT JOIN LATERAL (
                SELECT COUNT(*)::int AS count
                FROM messages m
                WHERE m.chat_id = c.id 
                  AND m.sender_id != @currentUserId 
                  AND m.is_read = FALSE
            ) unread ON true
            ORDER BY COALESCE(lm.created_at, c.updated_at) DESC;
            """);

        command.Parameters.AddWithValue("currentUserId", currentUserId);

        var chats = new List<ChatSummaryResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var chatId = reader.GetGuid(0);
            var type = reader.GetString(1);
            var title = reader.IsDBNull(2) ? null : reader.GetString(2);
            var avatarUrl = reader.IsDBNull(3) ? null : reader.GetString(3);
            var createdAt = ToDateTimeOffset(reader.GetValue(4));
            var updatedAt = ToDateTimeOffset(reader.GetValue(5));
            var myRole = reader.GetString(6);

            Guid? otherUserId = reader.IsDBNull(7) ? null : reader.GetGuid(7);
            var otherUserDisplayName = reader.IsDBNull(8) ? null : reader.GetString(8);
            var otherUserUsername = reader.IsDBNull(9) ? null : reader.GetString(9);
            var otherUserPhone = reader.IsDBNull(10) ? null : reader.GetString(10);

            if (type == ChatTypes.Private)
            {
                title = !string.IsNullOrWhiteSpace(otherUserDisplayName)
                    ? otherUserDisplayName
                    : !string.IsNullOrWhiteSpace(otherUserUsername)
                        ? otherUserUsername
                        : otherUserPhone ?? "Chat";
            }

            ChatLastMessageResponse? lastMessage = null;
            if (!reader.IsDBNull(11))
            {
                var msgId = reader.GetGuid(11);
                Guid? msgSenderId = reader.IsDBNull(12) ? null : reader.GetGuid(12);
                var senderDisplayName = reader.IsDBNull(13) ? null : reader.GetString(13);
                var senderUsername = reader.IsDBNull(14) ? null : reader.GetString(14);
                var senderPhone = reader.IsDBNull(15) ? null : reader.GetString(15);
                var isRead = reader.GetBoolean(18);
                var isDeleted = reader.GetBoolean(19);

                var content = isDeleted
                    ? "This message was deleted."
                    : reader.GetString(16);
                var msgCreatedAt = ToDateTimeOffset(reader.GetValue(17));

                var senderName = !string.IsNullOrWhiteSpace(senderDisplayName)
                    ? senderDisplayName
                    : !string.IsNullOrWhiteSpace(senderUsername)
                        ? senderUsername
                        : senderPhone ?? "User";

                lastMessage = new ChatLastMessageResponse(
                    msgId,
                    msgSenderId,
                    senderName,
                    content,
                    msgCreatedAt,
                    isRead,
                    isDeleted);
            }

            var unreadCount = reader.GetInt32(20);

            chats.Add(new ChatSummaryResponse(
                chatId,
                type,
                title,
                avatarUrl,
                createdAt,
                updatedAt,
                myRole,
                lastMessage,
                unreadCount,
                otherUserId,
                otherUserUsername,
                otherUserPhone,
                otherUserDisplayName));
        }

        return ServiceResult<List<ChatSummaryResponse>>.Success(chats);
    }

    public async Task<ServiceResult<ChatDetailsResponse>> GetChatDetailsAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT c.id, c.type::text, c.title, c.avatar_url, c.created_at, c.updated_at, cm.role::text
            FROM chats c
            JOIN chat_members cm ON cm.chat_id = c.id AND cm.user_id = @currentUserId
            WHERE c.id = @chatId;
            """);
        command.Parameters.AddWithValue("currentUserId", currentUserId);
        command.Parameters.AddWithValue("chatId", chatId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ServiceResult<ChatDetailsResponse>.NotFound("Chat not found or you are not a member.");
        }

        var type = reader.GetString(1);
        var title = reader.IsDBNull(2) ? null : reader.GetString(2);
        var avatarUrl = reader.IsDBNull(3) ? null : reader.GetString(3);
        var createdAt = ToDateTimeOffset(reader.GetValue(4));
        var updatedAt = ToDateTimeOffset(reader.GetValue(5));
        var myRole = reader.GetString(6);
        await reader.CloseAsync();

        var members = await LoadChatMembersInternalAsync(chatId, cancellationToken);

        Guid? otherMemberId = null;
        string? otherMemberUsername = null;
        string? otherMemberPhone = null;
        string? otherMemberDisplayName = null;

        if (type == ChatTypes.Private)
        {
            var otherMember = members.FirstOrDefault(m => m.UserId != currentUserId);
            if (otherMember is not null)
            {
                otherMemberId = otherMember.UserId;
                otherMemberUsername = otherMember.Username;
                otherMemberPhone = otherMember.PhoneNumber;
                otherMemberDisplayName = otherMember.DisplayName;

                title = !string.IsNullOrWhiteSpace(otherMember.DisplayName)
                    ? otherMember.DisplayName
                    : !string.IsNullOrWhiteSpace(otherMember.Username)
                        ? otherMember.Username
                        : otherMember.PhoneNumber;
            }
        }

        return ServiceResult<ChatDetailsResponse>.Success(new ChatDetailsResponse(
            chatId,
            type,
            title,
            avatarUrl,
            createdAt,
            updatedAt,
            myRole,
            members,
            otherMemberId,
            otherMemberUsername,
            otherMemberPhone,
            otherMemberDisplayName));
    }

    private async Task<List<ChatMemberResponse>> LoadChatMembersInternalAsync(
        Guid chatId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT cm.user_id, u.username, u.display_name, u.phone_number, cm.role::text, cm.joined_at
            FROM chat_members cm
            JOIN users u ON u.id = cm.user_id
            WHERE cm.chat_id = @chatId
            ORDER BY 
                CASE cm.role WHEN 'owner'::member_role THEN 1 WHEN 'admin'::member_role THEN 2 ELSE 3 END,
                cm.joined_at ASC;
            """);
        command.Parameters.AddWithValue("chatId", chatId);

        var members = new List<ChatMemberResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            members.Add(new ChatMemberResponse(
                reader.GetGuid(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return members;
    }

    private static DateTimeOffset ToDateTimeOffset(object? value) => value switch
    {
        DateTimeOffset dto => dto,
        DateTime dt => dt.Kind == DateTimeKind.Utc ? new DateTimeOffset(dt) : new DateTimeOffset(dt, TimeSpan.Zero),
        _ => DateTimeOffset.UtcNow
    };
}
