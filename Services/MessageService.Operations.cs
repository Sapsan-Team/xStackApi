using Npgsql;
using xstackApi.Hubs;
using xstackApi.Models;

namespace xstackApi.Services;

public sealed partial class MessageService
{
    public async Task<ServiceResult<MessageResponse>> SendMessageAsync(
        Guid chatId,
        Guid currentUserId,
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var content = request.Content.Trim();
        if (string.IsNullOrEmpty(content))
        {
            return ServiceResult<MessageResponse>.BadRequest("Message content cannot be empty.");
        }

        string senderName;
        string? senderUsername;

        await using (var checkCmd = dataSource.CreateCommand(
            """
            SELECT u.display_name, u.username, u.phone_number
            FROM chat_members cm
            JOIN users u ON u.id = cm.user_id
            WHERE cm.chat_id = @chatId AND cm.user_id = @currentUserId;
            """))
        {
            checkCmd.Parameters.AddWithValue("chatId", chatId);
            checkCmd.Parameters.AddWithValue("currentUserId", currentUserId);

            await using var reader = await checkCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ServiceResult<MessageResponse>.Forbidden("You are not a member of this chat.");
            }

            var displayName = reader.IsDBNull(0) ? null : reader.GetString(0);
            senderUsername = reader.IsDBNull(1) ? null : reader.GetString(1);
            var phoneNumber = reader.GetString(2);
            senderName = displayName ?? senderUsername ?? phoneNumber;
        }

        var messageId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        DateTimeOffset createdAt;
        DateTimeOffset updatedAt;

        await using (var insertCmd = new NpgsqlCommand(
            """
            INSERT INTO messages (id, chat_id, sender_id, content, is_read, is_edited, is_deleted)
            VALUES (@id, @chatId, @senderId, @content, FALSE, FALSE, FALSE)
            RETURNING created_at, updated_at;
            """, connection, transaction))
        {
            insertCmd.Parameters.AddWithValue("id", messageId);
            insertCmd.Parameters.AddWithValue("chatId", chatId);
            insertCmd.Parameters.AddWithValue("senderId", currentUserId);
            insertCmd.Parameters.AddWithValue("content", content);

            await using var reader = await insertCmd.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            createdAt = reader.GetFieldValue<DateTimeOffset>(0);
            updatedAt = reader.GetFieldValue<DateTimeOffset>(1);
        }

        await using (var updateChatCmd = new NpgsqlCommand(
            "UPDATE chats SET updated_at = CURRENT_TIMESTAMP WHERE id = @chatId;",
            connection, transaction))
        {
            updateChatCmd.Parameters.AddWithValue("chatId", chatId);
            await updateChatCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var updateMemberCmd = new NpgsqlCommand(
            "UPDATE chat_members SET last_read_message_id = @messageId WHERE chat_id = @chatId AND user_id = @userId;",
            connection, transaction))
        {
            updateMemberCmd.Parameters.AddWithValue("messageId", messageId);
            updateMemberCmd.Parameters.AddWithValue("chatId", chatId);
            updateMemberCmd.Parameters.AddWithValue("userId", currentUserId);
            await updateMemberCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var messageResponse = new MessageResponse(
            messageId,
            chatId,
            currentUserId,
            senderName,
            senderUsername,
            content,
            false,
            false,
            false,
            createdAt,
            updatedAt,
            true);

        await hubContext.Clients.Group(ChatHub.GetChatGroupName(chatId))
            .ReceiveMessage(messageResponse);

        return ServiceResult<MessageResponse>.Success(messageResponse);
    }

    public async Task<ServiceResult<MessageResponse>> EditMessageAsync(
        Guid chatId,
        Guid messageId,
        Guid currentUserId,
        EditMessageRequest request,
        CancellationToken cancellationToken)
    {
        var content = request.Content.Trim();
        if (string.IsNullOrEmpty(content))
        {
            return ServiceResult<MessageResponse>.BadRequest("Message content cannot be empty.");
        }

        bool isRead;
        DateTimeOffset createdAt;
        string senderName;
        string? senderUsername;

        await using (var checkCmd = dataSource.CreateCommand(
            """
            SELECT m.sender_id, m.is_deleted, m.is_read, m.created_at, u.display_name, u.username, u.phone_number
            FROM messages m
            LEFT JOIN users u ON u.id = m.sender_id
            WHERE m.id = @messageId AND m.chat_id = @chatId;
            """))
        {
            checkCmd.Parameters.AddWithValue("messageId", messageId);
            checkCmd.Parameters.AddWithValue("chatId", chatId);

            await using var reader = await checkCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ServiceResult<MessageResponse>.NotFound("Message not found.");
            }

            var senderId = reader.IsDBNull(0) ? (Guid?)null : reader.GetGuid(0);
            var isDeleted = reader.GetBoolean(1);
            isRead = reader.GetBoolean(2);
            createdAt = reader.GetFieldValue<DateTimeOffset>(3);
            var displayName = reader.IsDBNull(4) ? null : reader.GetString(4);
            senderUsername = reader.IsDBNull(5) ? null : reader.GetString(5);
            var phoneNumber = reader.IsDBNull(6) ? null : reader.GetString(6);
            senderName = displayName ?? senderUsername ?? phoneNumber ?? "User";

            if (senderId != currentUserId)
            {
                return ServiceResult<MessageResponse>.Forbidden("Only the author can edit this message.");
            }

            if (isDeleted)
            {
                return ServiceResult<MessageResponse>.BadRequest("Cannot edit a deleted message.");
            }
        }

        await using var updateCmd = dataSource.CreateCommand(
            """
            UPDATE messages 
            SET content = @content, is_edited = TRUE, updated_at = CURRENT_TIMESTAMP 
            WHERE id = @messageId
            RETURNING updated_at;
            """);
        updateCmd.Parameters.AddWithValue("content", content);
        updateCmd.Parameters.AddWithValue("messageId", messageId);

        var scalarResult = await updateCmd.ExecuteScalarAsync(cancellationToken);
        var updatedAt = ToDateTimeOffset(scalarResult);

        var editedResponse = new MessageResponse(
            messageId,
            chatId,
            currentUserId,
            senderName,
            senderUsername,
            content,
            isRead,
            true,
            false,
            createdAt,
            updatedAt,
            true);

        await hubContext.Clients.Group(ChatHub.GetChatGroupName(chatId))
            .MessageEdited(editedResponse);

        return ServiceResult<MessageResponse>.Success(editedResponse);
    }

    public async Task<ServiceResult> DeleteMessageAsync(
        Guid chatId,
        Guid messageId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        await using var checkCmd = dataSource.CreateCommand(
            """
            SELECT m.sender_id, m.is_deleted, cm.role::text
            FROM messages m
            JOIN chat_members cm ON cm.chat_id = m.chat_id AND cm.user_id = @currentUserId
            WHERE m.id = @messageId AND m.chat_id = @chatId;
            """);
        checkCmd.Parameters.AddWithValue("messageId", messageId);
        checkCmd.Parameters.AddWithValue("chatId", chatId);
        checkCmd.Parameters.AddWithValue("currentUserId", currentUserId);

        await using (var reader = await checkCmd.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ServiceResult.NotFound("Message not found or you are not a member of this chat.");
            }

            var senderId = reader.IsDBNull(0) ? (Guid?)null : reader.GetGuid(0);
            var isDeleted = reader.GetBoolean(1);
            var role = reader.GetString(2);

            if (isDeleted)
            {
                return ServiceResult.Success();
            }

            if (senderId != currentUserId && role != MemberRoles.Owner && role != MemberRoles.Admin)
            {
                return ServiceResult.Forbidden("You do not have permission to delete this message.");
            }
        }

        await using var deleteCmd = dataSource.CreateCommand(
            """
            UPDATE messages 
            SET is_deleted = TRUE, content = NULL, updated_at = CURRENT_TIMESTAMP 
            WHERE id = @messageId;
            """);
        deleteCmd.Parameters.AddWithValue("messageId", messageId);
        await deleteCmd.ExecuteNonQueryAsync(cancellationToken);

        await hubContext.Clients.Group(ChatHub.GetChatGroupName(chatId))
            .MessageDeleted(chatId, messageId);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult<MarkReadResponse>> MarkAsReadAsync(
        Guid chatId,
        Guid currentUserId,
        MarkMessagesReadRequest request,
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
                return ServiceResult<MarkReadResponse>.NotFound("Chat not found or access denied.");
            }
        }

        Guid targetMessageId;
        DateTimeOffset targetCreatedAt;

        if (request.MessageId.HasValue)
        {
            await using var msgCmd = dataSource.CreateCommand(
                "SELECT id, created_at FROM messages WHERE id = @id AND chat_id = @chatId;");
            msgCmd.Parameters.AddWithValue("id", request.MessageId.Value);
            msgCmd.Parameters.AddWithValue("chatId", chatId);

            await using var reader = await msgCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ServiceResult<MarkReadResponse>.NotFound("Specified message not found in this chat.");
            }

            targetMessageId = reader.GetGuid(0);
            targetCreatedAt = reader.GetFieldValue<DateTimeOffset>(1);
        }
        else
        {
            await using var latestCmd = dataSource.CreateCommand(
                "SELECT id, created_at FROM messages WHERE chat_id = @chatId ORDER BY created_at DESC LIMIT 1;");
            latestCmd.Parameters.AddWithValue("chatId", chatId);

            await using var reader = await latestCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ServiceResult<MarkReadResponse>.Success(new MarkReadResponse(0, null));
            }

            targetMessageId = reader.GetGuid(0);
            targetCreatedAt = reader.GetFieldValue<DateTimeOffset>(1);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var updateMessagesCmd = new NpgsqlCommand(
            """
            UPDATE messages
            SET is_read = TRUE
            WHERE chat_id = @chatId 
              AND sender_id != @currentUserId 
              AND created_at <= @targetCreatedAt
              AND is_read = FALSE;
            """, connection, transaction))
        {
            updateMessagesCmd.Parameters.AddWithValue("chatId", chatId);
            updateMessagesCmd.Parameters.AddWithValue("currentUserId", currentUserId);
            updateMessagesCmd.Parameters.AddWithValue("targetCreatedAt", targetCreatedAt);
            await updateMessagesCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var updateMemberCmd = new NpgsqlCommand(
            """
            UPDATE chat_members
            SET last_read_message_id = @targetMessageId
            WHERE chat_id = @chatId AND user_id = @currentUserId;
            """, connection, transaction))
        {
            updateMemberCmd.Parameters.AddWithValue("targetMessageId", targetMessageId);
            updateMemberCmd.Parameters.AddWithValue("chatId", chatId);
            updateMemberCmd.Parameters.AddWithValue("currentUserId", currentUserId);
            await updateMemberCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        int unreadCount;
        await using (var countCmd = new NpgsqlCommand(
            """
            SELECT COUNT(*)::int
            FROM messages
            WHERE chat_id = @chatId 
              AND sender_id != @currentUserId 
              AND is_read = FALSE;
            """, connection, transaction))
        {
            countCmd.Parameters.AddWithValue("chatId", chatId);
            countCmd.Parameters.AddWithValue("currentUserId", currentUserId);
            unreadCount = (int)(await countCmd.ExecuteScalarAsync(cancellationToken))!;
        }

        await transaction.CommitAsync(cancellationToken);

        await hubContext.Clients.Group(ChatHub.GetChatGroupName(chatId))
            .MessagesRead(chatId, currentUserId, targetMessageId);

        return ServiceResult<MarkReadResponse>.Success(new MarkReadResponse(unreadCount, targetMessageId));
    }
}
