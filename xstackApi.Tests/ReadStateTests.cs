using xstackApi.Services;

namespace xstackApi.Tests;

public sealed class ReadStateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddMinutes(1);
    private static readonly DateTimeOffset T2 = T0.AddMinutes(2);

    [Fact]
    public void Incoming_Message_Is_Unread_Before_The_Users_Cursor()
    {
        Assert.False(ReadState.IsRead(
            isMine: false,
            createdAt: T1,
            myReadAt: T0,
            othersMinReadAt: null));
    }

    [Fact]
    public void Incoming_Message_Is_Read_Up_To_The_Users_Cursor()
    {
        Assert.True(ReadState.IsRead(
            isMine: false,
            createdAt: T1,
            myReadAt: T1,
            othersMinReadAt: null));
    }

    [Fact]
    public void Incoming_Message_Stays_Unread_When_Nothing_Was_Read()
    {
        Assert.False(ReadState.IsRead(
            isMine: false,
            createdAt: T1,
            myReadAt: null,
            othersMinReadAt: null));
    }

    [Fact]
    public void Own_Message_Is_Read_Only_When_Every_Other_Member_Is_Past_It()
    {
        Assert.True(ReadState.IsRead(
            isMine: true,
            createdAt: T1,
            myReadAt: null,
            othersMinReadAt: T2));

        Assert.False(ReadState.IsRead(
            isMine: true,
            createdAt: T2,
            myReadAt: null,
            othersMinReadAt: T1));
    }

    [Fact]
    public void Own_Message_Is_Unread_When_A_Member_Has_Read_Nothing()
    {
        // othersMinReadAt is null when at least one other member has no cursor.
        Assert.False(ReadState.IsRead(
            isMine: true,
            createdAt: T0,
            myReadAt: T2,
            othersMinReadAt: null));
    }

    [Fact]
    public void Own_Message_Is_Unread_In_A_Chat_Without_Other_Members()
    {
        Assert.False(ReadState.IsRead(
            isMine: true,
            createdAt: T0,
            myReadAt: T2,
            othersMinReadAt: null));
    }
}
