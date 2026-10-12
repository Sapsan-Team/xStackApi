using Npgsql;
using xstackApi.Models;

namespace xstackApi.Services;

public sealed class UserService(NpgsqlDataSource dataSource) : IUserService
{
    public async Task<ServiceResult<List<UserSearchResultResponse>>> SearchUsersAsync(
        string query,
        Guid currentUserId,
        int limit,
        CancellationToken cancellationToken)
    {
        var trimmed = query.Trim();
        var cleanQuery = trimmed.StartsWith('@') ? trimmed[1..] : trimmed;
        var digitsOnly = new string(trimmed.Where(char.IsDigit).ToArray());

        var clampedLimit = Math.Clamp(limit, 1, 50);

        if (string.IsNullOrEmpty(trimmed))
        {
            await using var allCmd = dataSource.CreateCommand(
                """
                SELECT id, phone_number, username, display_name, bio
                FROM users
                WHERE id != @currentUserId 
                ORDER BY updated_at DESC
                LIMIT @limit;
                """);
            allCmd.Parameters.AddWithValue("currentUserId", currentUserId);
            allCmd.Parameters.AddWithValue("limit", clampedLimit);

            var allResults = new List<UserSearchResultResponse>();
            await using var allReader = await allCmd.ExecuteReaderAsync(cancellationToken);
            while (await allReader.ReadAsync(cancellationToken))
            {
                allResults.Add(new UserSearchResultResponse(
                    allReader.GetGuid(0),
                    allReader.GetString(1),
                    allReader.IsDBNull(2) ? null : allReader.GetString(2),
                    allReader.IsDBNull(3) ? null : allReader.GetString(3),
                    allReader.IsDBNull(4) ? null : allReader.GetString(4)));
            }
            return ServiceResult<List<UserSearchResultResponse>>.Success(allResults);
        }

        await using var command = dataSource.CreateCommand(
            """
            SELECT id, phone_number, username, display_name, bio
            FROM users
            WHERE id != @currentUserId 
              AND (
                  phone_number ILIKE @likeQuery 
                  OR (@digitsOnly != '' AND phone_number ILIKE @digitsLikeQuery)
                  OR username ILIKE @likeQuery 
                  OR (@cleanQuery != '' AND username ILIKE @cleanLikeQuery)
                  OR display_name ILIKE @likeQuery
                  OR (@cleanQuery != '' AND display_name ILIKE @cleanLikeQuery)
              )
            ORDER BY 
              CASE 
                WHEN username ILIKE @cleanQuery THEN 1
                WHEN phone_number ILIKE @rawQuery THEN 2
                WHEN display_name ILIKE @rawQuery THEN 3
                ELSE 4
              END,
              username ASC NULLS LAST
            LIMIT @limit;
            """);

        command.Parameters.AddWithValue("currentUserId", currentUserId);
        command.Parameters.AddWithValue("likeQuery", $"%{trimmed}%");
        command.Parameters.AddWithValue("rawQuery", trimmed);
        command.Parameters.AddWithValue("cleanQuery", cleanQuery);
        command.Parameters.AddWithValue("cleanLikeQuery", $"%{cleanQuery}%");
        command.Parameters.AddWithValue("digitsOnly", digitsOnly);
        command.Parameters.AddWithValue("digitsLikeQuery", $"%{digitsOnly}%");
        command.Parameters.AddWithValue("limit", clampedLimit);

        var results = new List<UserSearchResultResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new UserSearchResultResponse(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return ServiceResult<List<UserSearchResultResponse>>.Success(results);
    }

    public async Task<ServiceResult<UserProfileResponse>> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT id, phone_number, username, display_name, bio, created_at
            FROM users
            WHERE id = @id;
            """);
        command.Parameters.AddWithValue("id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ServiceResult<UserProfileResponse>.NotFound("User profile not found.");
        }

        var profile = new UserProfileResponse(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetFieldValue<DateTimeOffset>(5));

        return ServiceResult<UserProfileResponse>.Success(profile);
    }
}
