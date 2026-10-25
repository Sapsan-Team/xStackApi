using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using xstackApi.Endpoints;
using xstackApi.Models;
using xstackApi.Services;

namespace xstackApi.Hubs;

[Authorize]
public class ChatHub : Hub<IChatClient>
{
    private readonly IUserService _userService;
    private readonly IChatService _chatService;
    private readonly IChatRoomService _chatRoomService;

    public ChatHub(
        IUserService userService,
        IChatService chatService,
        IChatRoomService chatRoomService)
    {
        _userService = userService;
        _chatService = chatService;
        _chatRoomService = chatRoomService;
    }

    public static string GetChatGroupName(Guid chatId) => $"chat:{chatId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.GetUserId();
        if (userId.HasValue)
        {
            _chatRoomService.TrackConnection(userId.Value, Context.ConnectionId);

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
            _chatRoomService.UntrackConnection(userId.Value, Context.ConnectionId);

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
        var userId = Context.User?.GetUserId();
        if (!userId.HasValue)
        {
            throw new HubException("Unauthorized.");
        }

        if (!await _chatService.IsMemberAsync(chatId, userId.Value, Context.ConnectionAborted))
        {
            throw new HubException("You are not a member of this chat.");
        }

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

        if (!await _chatService.IsMemberAsync(chatId, userId.Value, Context.ConnectionAborted))
        {
            return;
        }

        // The JWT carries no name claim, so resolve the nickname from the
        // database instead of falling back to a generic "User".
        var profileResult = await _userService.GetProfileAsync(
            userId.Value,
            Context.ConnectionAborted);
        var displayName = !string.IsNullOrEmpty(profileResult.Data?.DisplayName)
            ? profileResult.Data.DisplayName
            : !string.IsNullOrEmpty(profileResult.Data?.Username)
                ? profileResult.Data.Username
                : "User";

        var groupName = GetChatGroupName(chatId);
        await Clients.OthersInGroup(groupName)
            .UserTyping(chatId, userId.Value, displayName);
    }
}
