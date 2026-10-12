using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using xstackApi.Endpoints;

namespace xstackApi.Hubs;

[Authorize]
public class ChatHub : Hub<IChatClient>
{
    public static string GetChatGroupName(Guid chatId) => $"chat:{chatId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.GetUserId();
        if (userId.HasValue)
        {
            // Broadcast that user is online to others
            await Clients.Others.UserPresenceChanged(userId.Value, true, null);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.GetUserId();
        if (userId.HasValue)
        {
            // Broadcast that user went offline
            await Clients.Others.UserPresenceChanged(userId.Value, false, DateTimeOffset.UtcNow);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join a specific chat room to receive live messages and typing updates.
    /// </summary>
    public async Task JoinChat(Guid chatId)
    {
        var groupName = GetChatGroupName(chatId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Leave a chat room when navigating away from it.
    /// </summary>
    public async Task LeaveChat(Guid chatId)
    {
        var groupName = GetChatGroupName(chatId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Broadcast a typing event to other members of the chat.
    /// </summary>
    public async Task SendTyping(Guid chatId)
    {
        var userId = Context.User?.GetUserId();
        if (!userId.HasValue)
        {
            return;
        }

        var displayName = Context.User?.FindFirstValue("display_name")
            ?? Context.User?.FindFirstValue(ClaimTypes.Name)
            ?? "User";

        var groupName = GetChatGroupName(chatId);
        await Clients.OthersInGroup(groupName).UserTyping(chatId, userId.Value, displayName);
    }
}
