using System.ComponentModel.DataAnnotations;

namespace xstackApi.Models;

public static class ChatTypes
{
    public const string Private = "private";
    public const string Group = "group";
}

public static class MemberRoles
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Member = "member";
}

/// <summary>Request to start or find an existing direct 1-to-1 chat with a user.</summary>
public sealed record CreateDirectChatRequest
{
    [Required]
    public Guid RecipientUserId { get; init; }
}

/// <summary>Request to create a new group chat.</summary>
public sealed record CreateGroupChatRequest
{
    [Required]
    [MaxLength(255)]
    [RegularExpression(@".*\S.*", ErrorMessage = "Title must not be whitespace.")]
    public string Title { get; init; } = string.Empty;

    public string? AvatarUrl { get; init; }

    public List<Guid>? MemberUserIds { get; init; }
}

/// <summary>Request to update group chat metadata.</summary>
public sealed record UpdateChatRequest
{
    [MaxLength(255)]
    public string? Title { get; init; }

    public string? AvatarUrl { get; init; }
}

/// <summary>Request to add a member to a group chat.</summary>
public sealed record AddChatMemberRequest
{
    [Required]
    public Guid UserId { get; init; }

    [MaxLength(20)]
    public string? Role { get; init; } = MemberRoles.Member;
}

/// <summary>Request to update a member's role in a group chat.</summary>
public sealed record UpdateMemberRoleRequest
{
    [Required]
    [MaxLength(20)]
    public string Role { get; init; } = string.Empty;
}

/// <summary>Brief summary of a chat for the chats list screen.</summary>
public sealed record ChatSummaryResponse(
    Guid Id,
    string Type,
    string? Title,
    string? AvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CurrentUserRole,
    ChatLastMessageResponse? LastMessage,
    int UnreadCount,
    Guid? OtherMemberId,
    string? OtherMemberUsername = null,
    string? OtherMemberPhone = null,
    string? OtherMemberDisplayName = null);

/// <summary>Detailed information about a chat including its members.</summary>
public sealed record ChatDetailsResponse(
    Guid Id,
    string Type,
    string? Title,
    string? AvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CurrentUserRole,
    List<ChatMemberResponse> Members,
    Guid? OtherMemberId = null,
    string? OtherMemberUsername = null,
    string? OtherMemberPhone = null,
    string? OtherMemberDisplayName = null);

/// <summary>Information about a chat member.</summary>
public sealed record ChatMemberResponse(
    Guid UserId,
    string? Username,
    string? DisplayName,
    string PhoneNumber,
    string Role,
    DateTimeOffset JoinedAt);
