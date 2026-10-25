using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace xstackApi.Services;

/// <summary>A stored refresh token, without the plaintext value.</summary>
public sealed record RefreshTokenRecord(Guid Id, Guid UserId, bool IsRevoked, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues, rotates and revokes opaque refresh tokens. Only a SHA-256 hash of
/// the token is persisted, so a database leak does not expose usable tokens.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Creates a token for the user and returns its plaintext value.</summary>
    Task<string> IssueAsync(Guid userId, string? deviceInfo, CancellationToken cancellationToken);

    /// <summary>
    /// Looks a token up by its hash. Returns null when the token was never
    /// issued. Expired or revoked tokens are returned so the caller can tell
    /// an expiry from a replay.
    /// </summary>
    Task<RefreshTokenRecord?> FindAsync(string token, CancellationToken cancellationToken);

    /// <summary>Revokes a single token. Returns false when it was already gone.</summary>
    Task<bool> RevokeAsync(Guid tokenId, CancellationToken cancellationToken);

    /// <summary>Revokes every token of the user (replay detected, or logout all).</summary>
    Task<int> RevokeAllAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class RefreshTokenService(NpgsqlDataSource dataSource) : IRefreshTokenService
{
    /// <summary>How long a refresh token stays usable before re-authentication.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public async Task<string> IssueAsync(
        Guid userId,
        string? deviceInfo,
        CancellationToken cancellationToken)
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var device = deviceInfo is { Length: > 255 } ? deviceInfo[..255] : deviceInfo;

        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO refresh_tokens (user_id, token_hash, device_info, expires_at)
            VALUES (@userId, @tokenHash, @deviceInfo, @expiresAt);
            """);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("tokenHash", Hash(token));
        command.Parameters.AddWithValue("deviceInfo", (object?)device ?? DBNull.Value);
        command.Parameters.AddWithValue("expiresAt", DateTimeOffset.UtcNow.Add(Lifetime));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return token;
    }

    public async Task<RefreshTokenRecord?> FindAsync(
        string token,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT id, user_id, is_revoked, expires_at
            FROM refresh_tokens
            WHERE token_hash = @tokenHash;
            """);
        command.Parameters.AddWithValue("tokenHash", Hash(token));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RefreshTokenRecord(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetBoolean(2),
            reader.GetFieldValue<DateTimeOffset>(3));
    }

    public async Task<bool> RevokeAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        // Guarded on is_revoked so two concurrent refreshes of the same token
        // cannot both succeed: only one of them flips the row.
        await using var command = dataSource.CreateCommand(
            """
            UPDATE refresh_tokens
            SET is_revoked = TRUE
            WHERE id = @id AND is_revoked = FALSE;
            """);
        command.Parameters.AddWithValue("id", tokenId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<int> RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            UPDATE refresh_tokens
            SET is_revoked = TRUE
            WHERE user_id = @userId AND is_revoked = FALSE;
            """);
        command.Parameters.AddWithValue("userId", userId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
