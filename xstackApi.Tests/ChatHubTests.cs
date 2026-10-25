using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using xstackApi.Hubs;
using xstackApi.Models;
using xstackApi.Services;

namespace xstackApi.Tests;

public sealed class ChatHubTests
{
    [Fact]
    public async Task JoinChat_Rejects_User_Who_Is_Not_A_Member()
    {
        var groups = new RecordingGroupManager();
        var hub = CreateHub(groups, isMember: false);

        await Assert.ThrowsAsync<HubException>(
            () => hub.JoinChat(Guid.NewGuid()));

        Assert.Empty(groups.AddedGroups);
    }

    [Fact]
    public async Task JoinChat_Adds_Member_To_Chat_Room()
    {
        var groups = new RecordingGroupManager();
        var chatId = Guid.NewGuid();
        var hub = CreateHub(groups, isMember: true);

        await hub.JoinChat(chatId);

        Assert.Equal([ChatHub.GetChatGroupName(chatId)], groups.AddedGroups);
    }

    [Fact]
    public async Task SendTyping_Is_Ignored_For_Non_Member()
    {
        var hub = CreateHub(new RecordingGroupManager(), isMember: false);

        // Must return before touching Clients/Groups: no broadcast is allowed.
        await hub.SendTyping(Guid.NewGuid());
    }

    private static ChatHub CreateHub(
        RecordingGroupManager groups,
        bool isMember)
    {
        return new ChatHub(
            userService: null!,
            chatService: new FakeChatService(isMember),
            chatRoomService: null!)
        {
            Context = new FakeHubCallerContext(Guid.NewGuid()),
            Groups = groups
        };
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public List<string> AddedGroups { get; } = [];

        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            AddedGroups.Add(groupName);
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHubCallerContext(Guid userId) : HubCallerContext
    {
        public override string ConnectionId => "test-connection";

        public override ClaimsPrincipal? User { get; } =
            new(new ClaimsIdentity([new Claim("sub", userId.ToString())]));

        public override string? UserIdentifier => userId.ToString();

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override IDictionary<object, object?> Items { get; } =
            new Dictionary<object, object?>();

        public override void Abort()
        {
        }
    }

    private sealed class FakeChatService(bool isMember) : IChatService
    {
        public Task<bool> IsMemberAsync(
            Guid chatId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(isMember);
        }

        public Task<List<Guid>> GetUserChatIdsAsync(
            Guid userId,
            CancellationToken cancellationToken) => Task.FromResult(new List<Guid>());

        public Task<ServiceResult<List<ChatSummaryResponse>>> GetUserChatsAsync(
            Guid currentUserId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult<ChatDetailsResponse>> GetChatDetailsAsync(
            Guid chatId,
            Guid currentUserId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult<ChatDetailsResponse>> CreateDirectChatAsync(
            Guid currentUserId,
            CreateDirectChatRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult<ChatDetailsResponse>> CreateGroupChatAsync(
            Guid currentUserId,
            CreateGroupChatRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult<ChatDetailsResponse>> UpdateChatAsync(
            Guid chatId,
            Guid currentUserId,
            UpdateChatRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult> DeleteChatAsync(
            Guid chatId,
            Guid currentUserId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult<List<ChatMemberResponse>>> GetChatMembersAsync(
            Guid chatId,
            Guid currentUserId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult<ChatMemberResponse>> AddMemberAsync(
            Guid chatId,
            Guid currentUserId,
            AddChatMemberRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult> RemoveMemberAsync(
            Guid chatId,
            Guid currentUserId,
            Guid targetUserId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult> UpdateMemberRoleAsync(
            Guid chatId,
            Guid currentUserId,
            Guid targetUserId,
            UpdateMemberRoleRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ServiceResult> LeaveChatAsync(
            Guid chatId,
            Guid currentUserId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
