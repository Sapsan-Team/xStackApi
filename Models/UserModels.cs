namespace xstackApi.Models;

/// <summary>Detailed public user profile.</summary>
public sealed record UserProfileResponse(
    Guid Id,
    string PhoneNumber,
    string? Username,
    string? DisplayName,
    string? Bio,
    DateTimeOffset CreatedAt);

/// <summary>Summary of a user found via search.</summary>
public sealed record UserSearchResultResponse(
    Guid Id,
    string PhoneNumber,
    string? Username,
    string? DisplayName,
    string? Bio);
