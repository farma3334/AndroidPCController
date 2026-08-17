using AndroidPCController.Core.Notifications;

namespace AndroidPCController.Tests;

public sealed class NotificationInfoParserTests
{
    [Fact]
    public void Parse_TypicalDump_ReturnsNotificationsWithTitleAndText()
    {
        var dump = """
                  Notification List:
                    NotificationRecord(0x09202f13: pkg=com.whatsapp user=UserHandle{0} id=42 tag=null importance=4 key=0|com.whatsapp|42|null|10237: Notification(channel=individual_chat_defaults_4 shortcut=null contentView=null vibrate=null sound=null defaults=0x0 flags=0x0 color=0x00000000 vis=PRIVATE))
                      uid=10237 userId=0
                      opPkg=com.whatsapp
                      flags=0x2
                      pri=0
                      when=1786926974222
                      extras={
                          android.title=String (WhatsApp)
                          android.text=String (Hey, how are you?)
                      }
                  Snoozed notifications:
                """;

        var result = NotificationInfoParser.Parse(dump);

        var notification = Assert.Single(result);
        Assert.Equal("com.whatsapp", notification.PackageName);
        Assert.Equal("WhatsApp", notification.Title);
        Assert.Equal("Hey, how are you?", notification.Text);
        Assert.Equal(1786926974222, notification.WhenMs);
        Assert.True(notification.IsOngoing);
    }

    [Fact]
    public void Parse_MultipleRecords_ParsesAll()
    {
        var dump = """
                    NotificationRecord(0x00000001: pkg=com.instagram.android user=UserHandle{0} id=7 tag=null importance=3 key=0|com.instagram.android|7|null|10153: Notification(channel=ig_direct shortcut=null contentView=null vibrate=null sound=null defaults=0x0 flags=0x0 color=0xff0095f6 vis=PRIVATE))
                  uid=10153 userId=0
                  flags=0x0
                  when=1786926000000
                  extras={
                      android.title=String (Instagram)
                      android.text=String (New message)
                  }
                NotificationRecord(0x00000002: pkg=android user=UserHandle{0} id=26 tag=null importance=4 key=0|android|26|null|1000: Notification(channel=DEVELOPER_IMPORTANT shortcut=null contentView=null vibrate=null sound=null defaults=0x0 flags=0x2 color=0x00000000 vis=PUBLIC))
                  uid=1000 userId=0
                  flags=0x2
                  when=1786925000000
                  extras={
                      android.title=String (USB debugging connected)
                      android.text=String (Tap to turn off USB debugging)
                  }
            """;

        var result = NotificationInfoParser.Parse(dump);

        Assert.Equal(2, result.Count);
        Assert.Equal("com.instagram.android", result[0].PackageName);
        Assert.Equal("New message", result[0].Text);
        Assert.False(result[0].IsOngoing);
        Assert.Equal("android", result[1].PackageName);
        Assert.Equal("USB debugging connected", result[1].Title);
        Assert.True(result[1].IsOngoing);
    }

    [Fact]
    public void Parse_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Empty(NotificationInfoParser.Parse(string.Empty));
        Assert.Empty(NotificationInfoParser.Parse(null!));
        Assert.Empty(NotificationInfoParser.Parse("no records here"));
    }

    [Fact]
    public void Parse_RedactedExtras_SkipsRecordWithoutCrashing()
    {
        var dump = """
                    NotificationRecord(0x00000003: pkg=com.someapp user=UserHandle{0} id=9 tag=null importance=2 key=0|com.someapp|9|null|1000: Notification(channel=ch shortcut=null contentView=null vibrate=null sound=null defaults=0x0 flags=0x0 color=0x00000000 vis=PRIVATE))
                  uid=1000 userId=0
                  flags=0x0
                  when=1786924000000
                  extras={
                      android.title=String [length=23]
                      android.text=String [length=52]
                  }
            """;

        var result = NotificationInfoParser.Parse(dump);

        var notification = Assert.Single(result);
        Assert.Equal("com.someapp", notification.PackageName);
        Assert.Equal(string.Empty, notification.Title);
        Assert.Equal(string.Empty, notification.Text);
    }

    [Fact]
    public void Parse_StatusBarNotificationSection_IsIgnored()
    {
        var dump = """
                    NotificationRecord(0x00000004: pkg=com.whatsapp user=UserHandle{0} id=1 tag=null importance=4 key=0|com.whatsapp|1|null|10237: Notification(channel=ch shortcut=null contentView=null vibrate=null sound=null defaults=0x0 flags=0x200 color=0xff1b8755 vis=PRIVATE))
                  uid=10237 userId=0
                  flags=0x200
                  when=1786923000000
                  extras={
                      android.title=String (WhatsApp)
                      android.text=String (Voice call)
                  }
                StatusBarNotification(pkg=com.whatsapp user=UserHandle{0} id=1 tag=null key=0|com.whatsapp|1|null|10237: Notification(channel=ch shortcut=null contentView=null vibrate=null sound=null defaults=0x0 flags=0x200 color=0xff1b8755 vis=PRIVATE))
                  ...
            """;

        var result = NotificationInfoParser.Parse(dump);

        var notification = Assert.Single(result);
        Assert.Equal("com.whatsapp", notification.PackageName);
        Assert.Equal("Voice call", notification.Text);
        Assert.False(notification.IsOngoing);
    }

    [Fact]
    public void NotificationInfo_DisplayTime_IsRelative()
    {
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        var info = new AndroidPCController.Core.Models.NotificationInfo(
            "com.whatsapp", "com.whatsapp", "t", "x", now - 120_000, false);

        Assert.Equal("2m ago", info.DisplayTime);
        Assert.Equal('C', info.IconChar);
        Assert.False(string.IsNullOrEmpty(info.IconColor));
        Assert.StartsWith("#", info.IconColor);
    }
}