using System.ComponentModel.DataAnnotations;

namespace xstackApi.Models;

/// <summary>Request to send a message to a chat.</summary>
public sealed record SendMessageRequest
{
    [Required]
    [MaxLength(4000)]
    [RegularExpression(@".*\S.*", ErrorMessage = "Message content must not be empty or whitespace.")]
    public string Content { get; init; } = string.Empty;
}

/// <summary>Request to edit a previously sent message.</summary>
public sealed record EditMessageRequest
{
    [Required]
    [MaxLength(4000)]
    [RegularExpression(@".*\S.*", ErrorMessage = "Message content must not be empty or whitespace.")]
    public string Content { get; init; } = string.Empty;
}

/// <summary>Request to mark messages in a chat as read up to a specified message ID.</summary>
public sealed record MarkMessagesReadRequest
{
    public Guid? MessageId { get; init; }
}

/// <summary>Representation of a message in a chat.</summary>
public sealed record MessageResponse(
    Guid Id,
    Guid ChatId,
    Guid? SenderId,
    string? SenderName,
    string? SenderUsername,
    string? Content,
    bool IsRead,
    bool IsEdited,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsMine);

/// <summary>Brief representation of the last message in a chat summary.</summary>
public sealed record ChatLastMessageResponse(
    Guid Id,
    Guid? SenderId,
    string? SenderName,
    string? Content,
    DateTimeOffset CreatedAt,
    bool IsRead,
    bool IsDeleted);

/// <summary>Response returned when marking messages as read.</summary>
public sealed record MarkReadResponse(
    int UnreadCount,
    Guid? LastReadMessageId);
