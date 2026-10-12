namespace xstackApi.Models;

/// <summary>Credentials and optional profile information for creating an account.</summary>
public sealed record RegisterRequest
{
    /// <summary>The user's phone number, up to 20 characters.</summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(20)]
    [System.ComponentModel.DataAnnotations.RegularExpression(@".*\S.*")]
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>A password of 8 to 128 characters.</summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(8)]
    [System.ComponentModel.DataAnnotations.MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    /// <summary>An optional unique username, up to 50 characters.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string? Username { get; init; }

    /// <summary>An optional display name, up to 100 characters.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? DisplayName { get; init; }
}

/// <summary>Phone number and password used to authenticate an account.</summary>
public sealed record LoginRequest
{
    /// <summary>The phone number associated with the account.</summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(20)]
    [System.ComponentModel.DataAnnotations.RegularExpression(@".*\S.*")]
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>The account password.</summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

/// <summary>A short-lived bearer access token returned after successful authentication.</summary>
public sealed record AuthTokenResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt);

/// <summary>The authenticated user's profile loaded from the database.</summary>
public sealed record AuthUserResponse(
    Guid Id,
    string PhoneNumber,
    string? Username,
    string? DisplayName,
    string? Bio);

public sealed record AuthUser(Guid Id, string PhoneNumber);

public sealed record JwtOptions(string Issuer, string Audience, byte[] SigningKey);
