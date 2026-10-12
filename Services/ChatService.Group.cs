using Npgsql;
using xstackApi.Models;

namespace xstackApi.Services;

public sealed partial class ChatService
{
    public async Task<ServiceResult<ChatDetailsResponse>> CreateGroupChatAsync(
        Guid currentUserId,
        CreateGroupChatRequest request,
        CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        if (string.IsNullOrEmpty(title))
        {
            return ServiceResult<ChatDetailsResponse>.BadRequest("Group chat title cannot be empty.");
        }

        var chatId = Guid.NewGuid();
        var avatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var insertChatCmd = new NpgsqlCommand(
            """
            INSERT INTO chats (id, type, title, avatar_url)
            VALUES (@id, 'group'::chat_type, @title, @avatarUrl);
            """, connection, transaction))
        {
            insertChatCmd.Parameters.AddWithValue("id", chatId);
            insertChatCmd.Parameters.AddWithValue("title", title);
            insertChatCmd.Parameters.AddWithValue("avatarUrl", (object?)avatarUrl ?? DBNull.Value);
            await insertChatCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertOwnerCmd = new NpgsqlCommand(
            """
            INSERT INTO chat_members (chat_id, user_id, role)
            VALUES (@chatId, @userId, 'owner'::member_role);
            """, connection, transaction))
        {
            insertOwnerCmd.Parameters.AddWithValue("chatId", chatId);
            insertOwnerCmd.Parameters.AddWithValue("userId", currentUserId);
            await insertOwnerCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        if (request.MemberUserIds is { Count: > 0 })
        {
            var memberIds = request.MemberUserIds
                .Where(id => id != currentUserId && id != Guid.Empty)
                .Distinct()
                .ToList();

            foreach (var memberId in memberIds)
            {
                await using var insertMemberCmd = new NpgsqlCommand(
                    """
                    INSERT INTO chat_members (chat_id, user_id, role)
                    SELECT @chatId, id, 'member'::member_role
                    FROM users
                    WHERE id = @memberId
                    ON CONFLICT DO NOTHING;
                    """, connection, transaction);
                insertMemberCmd.Parameters.AddWithValue("chatId", chatId);
                insertMemberCmd.Parameters.AddWithValue("memberId", memberId);
                await insertMemberCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return await GetChatDetailsAsync(chatId, currentUserId, cancellationToken);
    }

    public async Task<ServiceResult<ChatDetailsResponse>> UpdateChatAsync(
        Guid chatId,
        Guid currentUserId,
        UpdateChatRequest request,
        CancellationToken cancellationToken)
    {
        await using var checkCmd = dataSource.CreateCommand(
            """
            SELECT c.type::text, cm.role::text
            FROM chats c
            JOIN chat_members cm ON cm.chat_id = c.id AND cm.user_id = @currentUserId
            WHERE c.id = @chatId;
            """);
        checkCmd.Parameters.AddWithValue("chatId", chatId);
        checkCmd.Parameters.AddWithValue("currentUserId", currentUserId);

        await using (var reader = await checkCmd.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ServiceResult<ChatDetailsResponse>.NotFound("Chat not found or access denied.");
            }

            var type = reader.GetString(0);
            var role = reader.GetString(1);

            if (type != ChatTypes.Group)
            {
                return ServiceResult<ChatDetailsResponse>.BadRequest("Only group chats can be updated.");
            }

            if (role != MemberRoles.Owner && role != MemberRoles.Admin)
            {
                return ServiceResult<ChatDetailsResponse>.Forbidden("Only group owner or admin can update chat settings.");
            }
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        var normalizedAvatar = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();

        await using var updateCmd = dataSource.CreateCommand(
            """
            UPDATE chats
            SET title = COALESCE(@title::varchar, title),
                avatar_url = COALESCE(@avatarUrl::text, avatar_url),
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @chatId;
            """);
        updateCmd.Parameters.AddWithValue("chatId", chatId);
        updateCmd.Parameters.AddWithValue("title", (object?)normalizedTitle ?? DBNull.Value);
        updateCmd.Parameters.AddWithValue("avatarUrl", (object?)normalizedAvatar ?? DBNull.Value);
        await updateCmd.ExecuteNonQueryAsync(cancellationToken);

        return await GetChatDetailsAsync(chatId, currentUserId, cancellationToken);
    }

    public async Task<ServiceResult> DeleteChatAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        await using var checkCmd = dataSource.CreateCommand(
            """
            SELECT c.type::text, cm.role::text
            FROM chats c
            JOIN chat_members cm ON cm.chat_id = c.id AND cm.user_id = @currentUserId
            WHERE c.id = @chatId;
            """);
        checkCmd.Parameters.AddWithValue("chatId", chatId);
        checkCmd.Parameters.AddWithValue("currentUserId", currentUserId);

        await using (var reader = await checkCmd.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ServiceResult.NotFound("Chat not found or access denied.");
            }

            var type = reader.GetString(0);
            var role = reader.GetString(1);

            if (type == ChatTypes.Group && role != MemberRoles.Owner)
            {
                return ServiceResult.Forbidden("Only the chat owner can delete a group chat.");
            }
        }

        await using var deleteCmd = dataSource.CreateCommand("DELETE FROM chats WHERE id = @chatId;");
        deleteCmd.Parameters.AddWithValue("chatId", chatId);
        await deleteCmd.ExecuteNonQueryAsync(cancellationToken);

        return ServiceResult.Success();
    }
}
