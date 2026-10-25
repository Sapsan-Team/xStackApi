using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace xstackApi.Hubs;

/// <summary>
/// Tracks live SignalR connections per user and evicts users from chat rooms
/// when they lose access to a chat.
/// </summary>
/// <remarks>
/// This registry is in-memory, so it covers a single API instance. A
/// multi-node deployment needs a shared connection registry (e.g. Redis) or
/// delivery by user id instead of long-lived group membership.
/// </remarks>
public interface IChatRoomService
{
    void TrackConnection(Guid userId, string connectionId);

    void UntrackConnection(Guid userId, string connectionId);

    Task RemoveUserFromChatAsync(
        Guid userId,
        Guid chatId,
        CancellationToken cancellationToken = default);

    Task AddUserToChatAsync(
        Guid userId,
        Guid chatId,
        CancellationToken cancellationToken = default);
}

public sealed class ChatRoomService(
    IHubContext<ChatHub> hubContext,
    ILogger<ChatRoomService> logger) : IChatRoomService
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _connections = new();

    public void TrackConnection(Guid userId, string connectionId)
    {
        _connections
            .GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>())[connectionId] = 0;
    }

    public void UntrackConnection(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var connections))
        {
            return;
        }

        connections.TryRemove(connectionId, out _);
        if (connections.IsEmpty)
        {
            _connections.TryRemove(userId, out _);
        }
    }

    public async Task AddUserToChatAsync(
        Guid userId,
        Guid chatId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.TryGetValue(userId, out var connections))
        {
            return;
        }

        var groupName = ChatHub.GetChatGroupName(chatId);
        foreach (var connectionId in connections.Keys)
        {
            try
            {
                await hubContext.Groups.AddToGroupAsync(
                    connectionId,
                    groupName,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                // Live subscription is best-effort: the membership itself was
                // already persisted, and the client re-joins on reconnect.
                logger.LogWarning(
                    exception,
                    "Failed to subscribe connection {ConnectionId} to chat {ChatId}.",
                    connectionId,
                    chatId);
            }
        }
    }

    public async Task RemoveUserFromChatAsync(
        Guid userId,
        Guid chatId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.TryGetValue(userId, out var connections))
        {
            return;
        }

        var groupName = ChatHub.GetChatGroupName(chatId);
        foreach (var connectionId in connections.Keys)
        {
            try
            {
                await hubContext.Groups.RemoveFromGroupAsync(
                    connectionId,
                    groupName,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                // Eviction is best-effort: a failed disconnect must not roll
                // back the membership change that already succeeded in SQL.
                logger.LogWarning(
                    exception,
                    "Failed to evict connection {ConnectionId} from chat {ChatId}.",
                    connectionId,
                    chatId);
            }
        }
    }
}
