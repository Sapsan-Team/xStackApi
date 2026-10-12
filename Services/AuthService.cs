using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using xstackApi.Models;

namespace xstackApi.Services;

public sealed class AuthService(
    NpgsqlDataSource dataSource,
    IPasswordHasher<AuthUser> passwordHasher,
    IJwtTokenService tokenService) : IAuthService
{
    private readonly string _dummyPasswordHash = passwordHasher.HashPassword(
        new AuthUser(Guid.Empty, string.Empty),
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public async Task<AuthTokenResponse?> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var user = new AuthUser(Guid.NewGuid(), request.PhoneNumber.Trim());
        var passwordHash = passwordHasher.HashPassword(user, request.Password);

        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO users (id, phone_number, username, display_name, password_hash)
            VALUES (@id, @phoneNumber, @username, @displayName, @passwordHash)
            """);
        command.Parameters.AddWithValue("id", user.Id);
        command.Parameters.AddWithValue("phoneNumber", user.PhoneNumber);
        command.Parameters.AddWithValue("username", (object?)NormalizeOptional(request.Username) ?? DBNull.Value);
        command.Parameters.AddWithValue("displayName", (object?)NormalizeOptional(request.DisplayName) ?? DBNull.Value);
        command.Parameters.AddWithValue("passwordHash", passwordHash);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return null;
        }

        return tokenService.CreateToken(user);
    }

    public async Task<AuthTokenResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT id, phone_number, password_hash
            FROM users
            WHERE phone_number = @phoneNumber
            """);
        command.Parameters.AddWithValue("phoneNumber", request.PhoneNumber.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            passwordHasher.VerifyHashedPassword(
                new AuthUser(Guid.Empty, string.Empty),
                _dummyPasswordHash,
                request.Password);
            return null;
        }

        var user = new AuthUser(reader.GetGuid(0), reader.GetString(1));
        var passwordHash = reader.GetString(2);
        var verification = passwordHasher.VerifyHashedPassword(user, passwordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var newHash = passwordHasher.HashPassword(user, request.Password);
            await using var updateCommand = dataSource.CreateCommand(
                "UPDATE users SET password_hash = @passwordHash WHERE id = @id");
            updateCommand.Parameters.AddWithValue("passwordHash", newHash);
            updateCommand.Parameters.AddWithValue("id", user.Id);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        return tokenService.CreateToken(user);
    }

    public async Task<AuthUserResponse?> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT id, phone_number, username, display_name, bio
            FROM users
            WHERE id = @id
            """);
        command.Parameters.AddWithValue("id", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AuthUserResponse(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
