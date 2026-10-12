using Npgsql;
using xstackApi.Models;

namespace xstackApi.Services;

public sealed partial class ChatService
{
    public async Task<ServiceResult<ChatDetailsResponse>> CreateDirectChatAsync(
        Guid currentUserId,
        CreateDirectChatRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserId == request.RecipientUserId)
        {
            return ServiceResult<ChatDetailsResponse>.BadRequest("Cannot create a direct chat with yourself.");
        }

        await using (var checkUserCommand = dataSource.CreateCommand("SELECT id FROM users WHERE id = @id;"))
        {
            checkUserCommand.Parameters.AddWithValue("id", request.RecipientUserId);
            var exists = await checkUserCommand.ExecuteScalarAsync(cancellationToken);
            if (exists is null)
            {
                return ServiceResult<ChatDetailsResponse>.NotFound("Recipient user not found.");
            }
        }

        await using (var findChatCommand = dataSource.CreateCommand(
            """
            SELECT c.id
            FROM chats c
            JOIN chat_members cm1 ON cm1.chat_id = c.id AND cm1.user_id = @currentUserId
            JOIN chat_members cm2 ON cm2.chat_id = c.id AND cm2.user_id = @recipientUserId
            WHERE c.type = 'private'::chat_type
            LIMIT 1;
            """))
        {
            findChatCommand.Parameters.AddWithValue("currentUserId", currentUserId);
            findChatCommand.Parameters.AddWithValue("recipientUserId", request.RecipientUserId);
            var existingId = await findChatCommand.ExecuteScalarAsync(cancellationToken);
            if (existingId is Guid existingChatId)
            {
                return await GetChatDetailsAsync(existingChatId, currentUserId, cancellationToken);
            }
        }

        var chatId = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var insertChatCmd = new NpgsqlCommand(
            "INSERT INTO chats (id, type) VALUES (@id, 'private'::chat_type);", connection, transaction))
        {
            insertChatCmd.Parameters.AddWithValue("id", chatId);
            await insertChatCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertMembersCmd = new NpgsqlCommand(
            """
            INSERT INTO chat_members (chat_id, user_id, role)
            VALUES 
                (@chatId, @currentUserId, 'member'::member_role),
                (@chatId, @recipientUserId, 'member'::member_role);
            """, connection, transaction))
        {
            insertMembersCmd.Parameters.AddWithValue("chatId", chatId);
            insertMembersCmd.Parameters.AddWithValue("currentUserId", currentUserId);
            insertMembersCmd.Parameters.AddWithValue("recipientUserId", request.RecipientUserId);
            await insertMembersCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return await GetChatDetailsAsync(chatId, currentUserId, cancellationToken);
    }
}
