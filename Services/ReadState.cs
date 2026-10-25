namespace xstackApi.Services;

/// <summary>
/// Per-user read state. Reading is tracked with the
/// <c>chat_members.last_read_message_id</c> cursor, so a message counts as read
/// for a member when it was created at or before that member's cursor.
/// </summary>
public static class ReadState
{
    /// <summary>
    /// Whether <paramref name="createdAt"/> is covered by a read cursor.
    /// </summary>
    /// <param name="isMine">The message was sent by the current user.</param>
    /// <param name="createdAt">Creation time of the message.</param>
    /// <param name="myReadAt">
    /// Creation time of the current user's read cursor, or null when they have
    /// not read anything in the chat yet.
    /// </param>
    /// <param name="othersMinReadAt">
    /// Earliest cursor among the other members, or null when at least one of
    /// them has read nothing (or the chat has no other members). Used for the
    /// read receipts of the current user's own messages.
    /// </param>
    public static bool IsRead(
        bool isMine,
        DateTimeOffset createdAt,
        DateTimeOffset? myReadAt,
        DateTimeOffset? othersMinReadAt)
    {
        // Own messages are "read" once every other member is past them;
        // incoming messages are "read" once the current user is past them.
        return isMine
            ? othersMinReadAt is { } others && createdAt <= others
            : myReadAt is { } mine && createdAt <= mine;
    }
}
