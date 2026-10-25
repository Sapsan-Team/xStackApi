using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using xstackApi.Hubs;

namespace xstackApi.Tests;

public sealed class ChatRoomServiceTests
{
    [Fact]
    public async Task RemoveUserFromChat_Evicts_Every_Tracked_Connection()
    {
        var groups = new RecordingGroupManager();
        var service = new ChatRoomService(
            new FakeHubContext(groups),
            NullLogger<ChatRoomService>.Instance);
        var userId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var groupName = ChatHub.GetChatGroupName(chatId);

        service.TrackConnection(userId, "connection-1");
        service.TrackConnection(userId, "connection-2");

        await service.RemoveUserFromChatAsync(userId, chatId);

        Assert.Equal(2, groups.Removed.Count);
        Assert.Contains(("connection-1", groupName), groups.Removed);
        Assert.Contains(("connection-2", groupName), groups.Removed);
    }

    [Fact]
    public async Task RemoveUserFromChat_Does_Nothing_For_Untracked_User()
    {
        var groups = new RecordingGroupManager();
        var service = new ChatRoomService(
            new FakeHubContext(groups),
            NullLogger<ChatRoomService>.Instance);

        await service.RemoveUserFromChatAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(groups.Removed);
    }

    private sealed class FakeHubContext(IGroupManager groups) : IHubContext<ChatHub>
    {
        public IHubClients Clients => throw new NotSupportedException();

        public IGroupManager Groups { get; } = groups;
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> Removed { get; } = [];

        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default)
        {
            Removed.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }
}
