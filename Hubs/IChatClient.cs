using xstackApi.Models;

namespace xstackApi.Hubs;

/// <summary>
/// Strongly-typed client contract for real-time SignalR notifications.
/// </summary>
public interface IChatClient
{
    /// <summary>Triggered when a new message is sent in the chat.</summary>
    Task ReceiveMessage(MessageResponse message);

    /// <summary>Triggered when a message is edited.</summary>
    Task MessageEdited(MessageResponse message);

    /// <summary>Triggered when a message is deleted (soft-delete).</summary>
    Task MessageDeleted(Guid chatId, Guid messageId);

    /// <summary>Triggered when messages are marked as read up to a message ID.</summary>
    Task MessagesRead(Guid chatId, Guid userId, Guid lastReadMessageId);

    /// <summary>Triggered when a user is actively typing in a chat room.</summary>
    Task UserTyping(Guid chatId, Guid userId, string displayName);

    /// <summary>Triggered when a user connects or disconnects.</summary>
    Task UserPresenceChanged(Guid userId, bool isOnline, DateTimeOffset? lastSeenAt);
}
