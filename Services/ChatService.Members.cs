using Npgsql;
using xstackApi.Models;

namespace xstackApi.Services;

public sealed partial class ChatService
{
    public async Task<bool> IsMemberAsync(
        Guid chatId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT 1 FROM chat_members WHERE chat_id = @chatId AND user_id = @userId;");
        command.Parameters.AddWithValue("chatId", chatId);
        command.Parameters.AddWithValue("userId", userId);

        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task<ServiceResult<List<ChatMemberResponse>>> GetChatMembersAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        await using var checkCmd = dataSource.CreateCommand(
            "SELECT 1 FROM chat_members WHERE chat_id = @chatId AND user_id = @currentUserId;");
        checkCmd.Parameters.AddWithValue("chatId", chatId);
        checkCmd.Parameters.AddWithValue("currentUserId", currentUserId);

        var isMember = await checkCmd.ExecuteScalarAsync(cancellationToken);
        if (isMember is null)
        {
            return ServiceResult<List<ChatMemberResponse>>.NotFound("Chat not found or access denied.");
        }

        var members = await LoadChatMembersInternalAsync(chatId, cancellationToken);
        return ServiceResult<List<ChatMemberResponse>>.Success(members);
    }

    public async Task<ServiceResult<ChatMemberResponse>> AddMemberAsync(
        Guid chatId,
        Guid currentUserId,
        AddChatMemberRequest request,
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
                return ServiceResult<ChatMemberResponse>.NotFound("Chat not found or access denied.");
            }

            var type = reader.GetString(0);
            var role = reader.GetString(1);

            if (type != ChatTypes.Group)
            {
                return ServiceResult<ChatMemberResponse>.BadRequest("Cannot add members to a private chat.");
            }

            if (role != MemberRoles.Owner && role != MemberRoles.Admin)
            {
                return ServiceResult<ChatMemberResponse>.Forbidden("Only group owner or admin can add members.");
            }
        }

        var roleToAssign = request.Role?.Trim().ToLowerInvariant() switch
        {
            MemberRoles.Admin => MemberRoles.Admin,
            _ => MemberRoles.Member
        };

        await using var checkTargetUserCmd = dataSource.CreateCommand("SELECT id FROM users WHERE id = @id;");
        checkTargetUserCmd.Parameters.AddWithValue("id", request.UserId);
        if (await checkTargetUserCmd.ExecuteScalarAsync(cancellationToken) is null)
        {
            return ServiceResult<ChatMemberResponse>.NotFound("User to add not found.");
        }

        await using var insertCmd = dataSource.CreateCommand(
            """
            INSERT INTO chat_members (chat_id, user_id, role)
            VALUES (@chatId, @userId, @role::member_role)
            ON CONFLICT (chat_id, user_id) DO UPDATE
            SET role = @role::member_role
            RETURNING joined_at;
            """);
        insertCmd.Parameters.AddWithValue("chatId", chatId);
        insertCmd.Parameters.AddWithValue("userId", request.UserId);
        insertCmd.Parameters.AddWithValue("role", roleToAssign);

        var joinedAtObj = await insertCmd.ExecuteScalarAsync(cancellationToken);
        var joinedAt = ToDateTimeOffset(joinedAtObj);

        await using var getUserInfoCmd = dataSource.CreateCommand(
            "SELECT username, display_name, phone_number FROM users WHERE id = @id;");
        getUserInfoCmd.Parameters.AddWithValue("id", request.UserId);

        await using (var reader = await getUserInfoCmd.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                return ServiceResult<ChatMemberResponse>.Success(new ChatMemberResponse(
                    request.UserId,
                    reader.IsDBNull(0) ? null : reader.GetString(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.GetString(2),
                    roleToAssign,
                    joinedAt));
            }
        }

        return ServiceResult<ChatMemberResponse>.NotFound("Failed to retrieve user details.");
    }

    public async Task<ServiceResult> RemoveMemberAsync(
        Guid chatId,
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        if (currentUserId == targetUserId)
        {
            return await LeaveChatAsync(chatId, currentUserId, cancellationToken);
        }

        await using var getRolesCmd = dataSource.CreateCommand(
            """
            SELECT 
                c.type::text,
                my_cm.role::text AS my_role,
                target_cm.role::text AS target_role
            FROM chats c
            JOIN chat_members my_cm ON my_cm.chat_id = c.id AND my_cm.user_id = @currentUserId
            LEFT JOIN chat_members target_cm ON target_cm.chat_id = c.id AND target_cm.user_id = @targetUserId
            WHERE c.id = @chatId;
            """);
        getRolesCmd.Parameters.AddWithValue("chatId", chatId);
        getRolesCmd.Parameters.AddWithValue("currentUserId", currentUserId);
        getRolesCmd.Parameters.AddWithValue("targetUserId", targetUserId);

        await using (var reader = await getRolesCmd.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ServiceResult.NotFound("Chat not found or access denied.");
            }

            var type = reader.GetString(0);
            if (type != ChatTypes.Group)
            {
                return ServiceResult.BadRequest("Cannot remove members from a private chat.");
            }

            var myRole = reader.GetString(1);
            if (reader.IsDBNull(2))
            {
                return ServiceResult.NotFound("Target member is not in this chat.");
            }

            var targetRole = reader.GetString(2);

            if (myRole != MemberRoles.Owner && myRole != MemberRoles.Admin)
            {
                return ServiceResult.Forbidden("Only group owner or admin can remove members.");
            }

            if (targetRole == MemberRoles.Owner)
            {
                return ServiceResult.Forbidden("Cannot remove the owner of the chat.");
            }

            if (myRole == MemberRoles.Admin && targetRole == MemberRoles.Admin)
            {
                return ServiceResult.Forbidden("Admins cannot remove other admins.");
            }
        }

        await using var deleteCmd = dataSource.CreateCommand(
            "DELETE FROM chat_members WHERE chat_id = @chatId AND user_id = @targetUserId;");
        deleteCmd.Parameters.AddWithValue("chatId", chatId);
        deleteCmd.Parameters.AddWithValue("targetUserId", targetUserId);
        await deleteCmd.ExecuteNonQueryAsync(cancellationToken);

        // Evict live connections immediately: otherwise a removed member would
        // keep receiving messages until they reconnect.
        await chatRoomService.RemoveUserFromChatAsync(targetUserId, chatId, CancellationToken.None);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> UpdateMemberRoleAsync(
        Guid chatId,
        Guid currentUserId,
        Guid targetUserId,
        UpdateMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var newRole = request.Role?.Trim().ToLowerInvariant();
        if (newRole != MemberRoles.Admin && newRole != MemberRoles.Member)
        {
            return ServiceResult.BadRequest("Role must be 'admin' or 'member'.");
        }

        await using var getRolesCmd = dataSource.CreateCommand(
            """
            SELECT 
                c.type::text,
                my_cm.role::text AS my_role,
                target_cm.role::text AS target_role
            FROM chats c
            JOIN chat_members my_cm ON my_cm.chat_id = c.id AND my_cm.user_id = @currentUserId
            LEFT JOIN chat_members target_cm ON target_cm.chat_id = c.id AND target_cm.user_id = @targetUserId
            WHERE c.id = @chatId;
            """);
        getRolesCmd.Parameters.AddWithValue("chatId", chatId);
        getRolesCmd.Parameters.AddWithValue("currentUserId", currentUserId);
        getRolesCmd.Parameters.AddWithValue("targetUserId", targetUserId);

        await using (var reader = await getRolesCmd.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ServiceResult.NotFound("Chat not found or access denied.");
            }

            var type = reader.GetString(0);
            if (type != ChatTypes.Group)
            {
                return ServiceResult.BadRequest("Roles can only be updated in group chats.");
            }

            var myRole = reader.GetString(1);
            if (myRole != MemberRoles.Owner)
            {
                return ServiceResult.Forbidden("Only the chat owner can change member roles.");
            }

            if (reader.IsDBNull(2))
            {
                return ServiceResult.NotFound("Target member is not in this chat.");
            }

            var targetRole = reader.GetString(2);
            if (targetRole == MemberRoles.Owner)
            {
                return ServiceResult.BadRequest("Cannot change the owner's role.");
            }
        }

        await using var updateCmd = dataSource.CreateCommand(
            """
            UPDATE chat_members 
            SET role = @role::member_role 
            WHERE chat_id = @chatId AND user_id = @targetUserId;
            """);
        updateCmd.Parameters.AddWithValue("chatId", chatId);
        updateCmd.Parameters.AddWithValue("targetUserId", targetUserId);
        updateCmd.Parameters.AddWithValue("role", newRole);
        await updateCmd.ExecuteNonQueryAsync(cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> LeaveChatAsync(
        Guid chatId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var checkCmd = new NpgsqlCommand(
            """
            SELECT c.type::text, cm.role::text
            FROM chats c
            JOIN chat_members cm ON cm.chat_id = c.id AND cm.user_id = @currentUserId
            WHERE c.id = @chatId;
            """, connection, transaction))
        {
            checkCmd.Parameters.AddWithValue("chatId", chatId);
            checkCmd.Parameters.AddWithValue("currentUserId", currentUserId);

            string type;
            string role;

            await using (var reader = await checkCmd.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return ServiceResult.NotFound("Chat not found or you are not a member.");
                }

                type = reader.GetString(0);
                role = reader.GetString(1);
            }

            if (role == MemberRoles.Owner)
            {
                Guid? nextOwnerId = null;
                await using (var findNextOwnerCmd = new NpgsqlCommand(
                    """
                    SELECT user_id 
                    FROM chat_members 
                    WHERE chat_id = @chatId AND user_id != @currentUserId 
                    ORDER BY 
                        CASE role WHEN 'admin'::member_role THEN 1 ELSE 2 END,
                        joined_at ASC 
                    LIMIT 1;
                    """, connection, transaction))
                {
                    findNextOwnerCmd.Parameters.AddWithValue("chatId", chatId);
                    findNextOwnerCmd.Parameters.AddWithValue("currentUserId", currentUserId);
                    var result = await findNextOwnerCmd.ExecuteScalarAsync(cancellationToken);
                    if (result is Guid nextId)
                    {
                        nextOwnerId = nextId;
                    }
                }

                if (nextOwnerId.HasValue)
                {
                    await using var promoteCmd = new NpgsqlCommand(
                        """
                        UPDATE chat_members 
                        SET role = 'owner'::member_role 
                        WHERE chat_id = @chatId AND user_id = @nextOwnerId;
                        """, connection, transaction);
                    promoteCmd.Parameters.AddWithValue("chatId", chatId);
                    promoteCmd.Parameters.AddWithValue("nextOwnerId", nextOwnerId.Value);
                    await promoteCmd.ExecuteNonQueryAsync(cancellationToken);

                    await using var removeCmd = new NpgsqlCommand(
                        "DELETE FROM chat_members WHERE chat_id = @chatId AND user_id = @currentUserId;",
                        connection, transaction);
                    removeCmd.Parameters.AddWithValue("chatId", chatId);
                    removeCmd.Parameters.AddWithValue("currentUserId", currentUserId);
                    await removeCmd.ExecuteNonQueryAsync(cancellationToken);
                }
                else
                {
                    await using var deleteChatCmd = new NpgsqlCommand(
                        "DELETE FROM chats WHERE id = @chatId;",
                        connection, transaction);
                    deleteChatCmd.Parameters.AddWithValue("chatId", chatId);
                    await deleteChatCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            else
            {
                await using var removeCmd = new NpgsqlCommand(
                    "DELETE FROM chat_members WHERE chat_id = @chatId AND user_id = @currentUserId;",
                    connection, transaction);
                removeCmd.Parameters.AddWithValue("chatId", chatId);
                removeCmd.Parameters.AddWithValue("currentUserId", currentUserId);
                await removeCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        // The chat may still exist for the other members, so make sure the
        // leaving user's live connection is no longer subscribed to it.
        await chatRoomService.RemoveUserFromChatAsync(currentUserId, chatId, CancellationToken.None);

        return ServiceResult.Success();
    }
}
