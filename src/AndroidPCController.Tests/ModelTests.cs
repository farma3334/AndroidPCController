using AndroidPCController.Core.Models;

namespace AndroidPCController.Tests;

public sealed class DeviceInfoTests
{
    [Fact]
    public void Creation_WithRequiredProperties_Succeeds()
    {
        var device = new DeviceInfo
        {
            Serial = "ABC123",
            Model = "Pixel 7",
            Manufacturer = "Google",
            AndroidVersion = "14",
            ApiLevel = 34,
            ProductName = "panther",
            DeviceName = "panther",
            ScreenWidth = 1080,
            ScreenHeight = 2400,
            ScreenDensity = 420,
            ConnectionState = "Online",
            ConnectionType = ConnectionType.Usb
        };

        Assert.Equal("ABC123", device.Serial);
        Assert.Equal("Pixel 7", device.Model);
        Assert.Equal(1080, device.ScreenWidth);
        Assert.Equal(2400, device.ScreenHeight);
        Assert.Equal(ConnectionType.Usb, device.ConnectionType);
    }

    [Fact]
    public void OptionalProperties_CanBeNull()
    {
        var device = new DeviceInfo
        {
            Serial = "X", Model = "X", Manufacturer = "X",
            AndroidVersion = "14", ApiLevel = 34,
            ProductName = "X", DeviceName = "X",
            ScreenWidth = 100, ScreenHeight = 100, ScreenDensity = 100,
            ConnectionState = "Online", ConnectionType = ConnectionType.Usb
        };

        Assert.Null(device.IpAddress);
        Assert.Null(device.BatteryLevel);
        Assert.Null(device.BatteryState);
        Assert.Null(device.Chipset);
    }

    [Fact]
    public void LastSeen_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow;

        var device = new DeviceInfo
        {
            Serial = "X", Model = "X", Manufacturer = "X",
            AndroidVersion = "14", ApiLevel = 34,
            ProductName = "X", DeviceName = "X",
            ScreenWidth = 100, ScreenHeight = 100, ScreenDensity = 100,
            ConnectionState = "Online", ConnectionType = ConnectionType.Usb
        };

        var after = DateTime.UtcNow;

        Assert.InRange(device.LastSeen, before, after);
    }
}

public sealed class DeviceCapabilitiesTests
{
    [Fact]
    public void Defaults_AllBooleansFalse()
    {
        var caps = new DeviceCapabilities();

        Assert.False(caps.ScreenStreaming);
        Assert.False(caps.AudioStreaming);
        Assert.False(caps.Clipboard);
        Assert.False(caps.FileTransfer);
        Assert.False(caps.RemoteInput);
        Assert.False(caps.Notifications);
        Assert.False(caps.ScreenRecording);
        Assert.False(caps.Screenshot);
        Assert.False(caps.ShellAccess);
        Assert.False(caps.InputInjection);
        Assert.False(caps.MediaProjection);
        Assert.False(caps.AccessibilityService);
        Assert.False(caps.H264Support);
        Assert.False(caps.H265Support);
    }

    [Fact]
    public void Defaults_MaxFpsIs60()
    {
        var caps = new DeviceCapabilities();

        Assert.Equal(60, caps.MaxFps);
    }

    [Fact]
    public void Defaults_MaxResolutionIs1920()
    {
        var caps = new DeviceCapabilities();

        Assert.Equal(1920, caps.MaxResolution);
    }

    [Fact]
    public void Properties_AreSettable()
    {
        var caps = new DeviceCapabilities
        {
            ScreenStreaming = true,
            H264Support = true,
            MaxFps = 120,
            MaxResolution = 2560
        };

        Assert.True(caps.ScreenStreaming);
        Assert.True(caps.H264Support);
        Assert.Equal(120, caps.MaxFps);
        Assert.Equal(2560, caps.MaxResolution);
    }
}

public sealed class StreamSettingsTests
{
    [Fact]
    public void ProtocolMessage_DefaultVersionIsOne()
    {
        var msg = new ProtocolMessage { MessageType = "TEST" };

        Assert.Equal(1, msg.Version);
    }

    [Fact]
    public void ProtocolMessage_TimestampDefaultsToUtcNow()
    {
        var before = DateTime.UtcNow;

        var msg = new ProtocolMessage { MessageType = "TEST" };

        var after = DateTime.UtcNow;
        Assert.InRange(msg.Timestamp, before, after);
    }

    [Fact]
    public void ProtocolMessage_AllMessageTypes_AreDefined()
    {
        Assert.Equal("HELLO", MessageTypes.Hello);
        Assert.Equal("GOODBYE", MessageTypes.Goodbye);
        Assert.Equal("PING", MessageTypes.Ping);
        Assert.Equal("PONG", MessageTypes.Pong);
        Assert.Equal("ERROR", MessageTypes.Error);
        Assert.Equal("INPUT_EVENT", MessageTypes.InputEvent);
        Assert.Equal("KEY_EVENT", MessageTypes.KeyEvent);
        Assert.Equal("TOUCH_EVENT", MessageTypes.TouchEvent);
        Assert.Equal("TEXT_EVENT", MessageTypes.TextEvent);
    }
}

public sealed class TransferProgressTests
{
    [Fact]
    public void StateTransitions_CanBeChanged()
    {
        var progress = new TransferProgress
        {
            TransferId = "t1",
            FileName = "test.apk",
            SourcePath = "/local/test.apk",
            DestinationPath = "/sdcard/test.apk",
            State = TransferState.Pending,
            TotalBytes = 1000
        };

        Assert.Equal(TransferState.Pending, progress.State);

        progress.State = TransferState.InProgress;
        Assert.Equal(TransferState.InProgress, progress.State);

        progress.State = TransferState.Completed;
        Assert.Equal(TransferState.Completed, progress.State);
    }

    [Fact]
    public void TransferredBytes_IsSettable()
    {
        var progress = new TransferProgress
        {
            TransferId = "t1",
            FileName = "file.dat",
            SourcePath = "/src",
            DestinationPath = "/dst",
            State = TransferState.InProgress,
            TotalBytes = 1000
        };

        progress.TransferredBytes = 500;
        Assert.Equal(500, progress.TransferredBytes);
    }

    [Fact]
    public void SpeedBytesPerSecond_IsSettable()
    {
        var progress = new TransferProgress
        {
            TransferId = "t1",
            FileName = "file.dat",
            SourcePath = "/src",
            DestinationPath = "/dst",
            State = TransferState.InProgress,
            TotalBytes = 1000
        };

        progress.SpeedBytesPerSecond = 1024.5;
        Assert.Equal(1024.5, progress.SpeedBytesPerSecond);
    }

    [Fact]
    public void ErrorMessage_IsSettable()
    {
        var progress = new TransferProgress
        {
            TransferId = "t1",
            FileName = "file.dat",
            SourcePath = "/src",
            DestinationPath = "/dst",
            State = TransferState.Failed,
            TotalBytes = 1000
        };

        progress.ErrorMessage = "Connection lost";
        Assert.Equal("Connection lost", progress.ErrorMessage);
    }

    [Fact]
    public void StartTime_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow;

        var progress = new TransferProgress
        {
            TransferId = "t1",
            FileName = "file.dat",
            SourcePath = "/src",
            DestinationPath = "/dst",
            State = TransferState.Pending,
            TotalBytes = 1000
        };

        var after = DateTime.UtcNow;
        Assert.InRange(progress.StartTime, before, after);
    }
}

public sealed class GameProfileTests
{
    [Fact]
    public void Creation_WithKeyMappings_Succeeds()
    {
        var mappings = new List<KeyMapping>
        {
            new() { KeyCode = 1, KeyName = "Fire", TouchX = 100, TouchY = 200 },
            new() { KeyCode = 2, KeyName = "Jump", TouchX = 300, TouchY = 400 }
        };
        var mouseMappings = new List<MouseMapping>
        {
            new() { Action = "LeftClick", TouchX = 500, TouchY = 500 }
        };

        var profile = new GameProfile
        {
            Id = "profile1",
            Name = "Test Profile",
            PackageName = "com.game.test",
            KeyMappings = mappings,
            MouseMappings = mouseMappings
        };

        Assert.Equal(2, profile.KeyMappings.Count);
        Assert.Single(profile.MouseMappings);
        Assert.Equal("com.game.test", profile.PackageName);
    }

    [Fact]
    public void Sensitivity_DefaultsToOne()
    {
        var profile = new GameProfile
        {
            Id = "p1", Name = "Test",
            KeyMappings = Array.Empty<KeyMapping>(),
            MouseMappings = Array.Empty<MouseMapping>()
        };

        Assert.Equal(1.0f, profile.Sensitivity);
    }

    [Fact]
    public void DeadZone_DefaultsTo10()
    {
        var profile = new GameProfile
        {
            Id = "p1", Name = "Test",
            KeyMappings = Array.Empty<KeyMapping>(),
            MouseMappings = Array.Empty<MouseMapping>()
        };

        Assert.Equal(10, profile.DeadZone);
    }

    [Fact]
    public void KeyMapping_ToggleIsFalseByDefault()
    {
        var mapping = new KeyMapping
        {
            KeyCode = 1, KeyName = "Test", TouchX = 0, TouchY = 0
        };

        Assert.False(mapping.IsToggle);
    }

    [Fact]
    public void KeyMapping_DurationMsDefaultsToZero()
    {
        var mapping = new KeyMapping
        {
            KeyCode = 1, KeyName = "Test", TouchX = 0, TouchY = 0
        };

        Assert.Equal(0, mapping.DurationMs);
    }

    [Fact]
    public void MouseMapping_DescriptionCanBeNull()
    {
        var mapping = new MouseMapping
        {
            Action = "LeftClick",
            TouchX = 10,
            TouchY = 20
        };

        Assert.Null(mapping.Description);
    }
}

public sealed class AutomationScriptTests
{
    [Fact]
    public void Creation_WithSteps_Succeeds()
    {
        var steps = new List<AutomationStep>
        {
            new() { Action = AutomationAction.Tap, X = 100, Y = 200 },
            new() { Action = AutomationAction.Wait, DelayMs = 500 },
            new() { Action = AutomationAction.PressKey, KeyCode = 3 }
        };

        var script = new AutomationScript
        {
            Id = "script1",
            Name = "Test Script",
            Steps = steps
        };

        Assert.Equal(3, script.Steps.Count);
        Assert.Equal(AutomationAction.Tap, script.Steps[0].Action);
    }

    [Fact]
    public void LoopForever_DefaultsToFalse()
    {
        var script = new AutomationScript
        {
            Id = "s1", Name = "Test",
            Steps = Array.Empty<AutomationStep>()
        };

        Assert.False(script.LoopForever);
    }

    [Fact]
    public void LoopCount_DefaultsToOne()
    {
        var script = new AutomationScript
        {
            Id = "s1", Name = "Test",
            Steps = Array.Empty<AutomationStep>()
        };

        Assert.Equal(1, script.LoopCount);
    }

    [Fact]
    public void AutomationStep_DelayMsDefaultsToZero()
    {
        var step = new AutomationStep { Action = AutomationAction.Tap, X = 0, Y = 0 };

        Assert.Equal(0, step.DelayMs);
    }

    [Fact]
    public void AutomationStep_AllActionTypes_AreDefined()
    {
        Assert.Equal(0, (int)AutomationAction.LaunchApp);
        Assert.Equal(1, (int)AutomationAction.Wait);
        Assert.Equal(2, (int)AutomationAction.Tap);
        Assert.Equal(5, (int)AutomationAction.PressKey);
        Assert.Equal(9, (int)AutomationAction.Back);
        Assert.Equal(10, (int)AutomationAction.Home);
        Assert.Equal(11, (int)AutomationAction.Recent);
        Assert.Equal(12, (int)AutomationAction.Sleep);
    }
}

public sealed class InputEventTests
{
    [Fact]
    public void InputEvent_Creation()
    {
        var evt = new InputEvent
        {
            Type = InputEventType.Tap,
            X = 100,
            Y = 200
        };

        Assert.Equal(InputEventType.Tap, evt.Type);
        Assert.Equal(100, evt.X);
        Assert.Equal(200, evt.Y);
    }

    [Fact]
    public void InputEvent_TimestampDefaultsToUtcNow()
    {
        var before = DateTime.UtcNow;

        var evt = new InputEvent
        {
            Type = InputEventType.KeyPress,
            X = 0,
            Y = 0
        };

        var after = DateTime.UtcNow;
        Assert.InRange(evt.Timestamp, before, after);
    }

    [Fact]
    public void KeyEvent_Creation()
    {
        var evt = new KeyEvent
        {
            KeyCode = 3,
            IsDown = true
        };

        Assert.Equal(3, evt.KeyCode);
        Assert.True(evt.IsDown);
    }

    [Fact]
    public void TextEvent_Creation()
    {
        var evt = new TextEvent { Text = "hello" };

        Assert.Equal("hello", evt.Text);
    }
}

public sealed class AppSettingsTests
{
    [Fact]
    public void Defaults_ThemeIsDark()
    {
        var settings = new AppSettings();
        Assert.Equal("Dark", settings.Theme);
    }

    [Fact]
    public void Defaults_LanguageIsEn()
    {
        var settings = new AppSettings();
        Assert.Equal("en", settings.Language);
    }

    [Fact]
    public void Defaults_StartMinimizedIsFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.StartMinimized);
    }

    [Fact]
    public void Defaults_MinimizeToTrayIsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.MinimizeToTray);
    }

    [Fact]
    public void Defaults_AutoReconnectIsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.AutoReconnect);
    }

    [Fact]
    public void Defaults_ConnectionTimeoutMsIs10000()
    {
        var settings = new AppSettings();
        Assert.Equal(10000, settings.ConnectionTimeoutMs);
    }

    [Fact]
    public void Defaults_DefaultFpsIs60()
    {
        var settings = new AppSettings();
        Assert.Equal(60, settings.DefaultFps);
    }

    [Fact]
    public void Defaults_DefaultBitrateIs8M()
    {
        var settings = new AppSettings();
        Assert.Equal(8_000_000, settings.DefaultBitrate);
    }

    [Fact]
    public void Defaults_DefaultResolutionIsNative()
    {
        var settings = new AppSettings();
        Assert.Equal("Native", settings.DefaultResolution);
    }

    [Fact]
    public void Defaults_DefaultCodecIsH264()
    {
        var settings = new AppSettings();
        Assert.Equal("H264", settings.DefaultCodec);
    }

    [Fact]
    public void Defaults_HardwareAccelerationIsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.HardwareAcceleration);
    }

    [Fact]
    public void Defaults_ClipboardSyncIsTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.ClipboardSync);
    }

    [Fact]
    public void Defaults_DownloadDirectoryContainsAndroidPCController()
    {
        var settings = new AppSettings();
        Assert.Contains("AndroidPCController", settings.DownloadDirectory);
    }

    [Fact]
    public void Properties_AreSettable()
    {
        var settings = new AppSettings
        {
            Theme = "Light",
            Language = "fr",
            StartMinimized = true,
            DefaultFps = 30
        };

        Assert.Equal("Light", settings.Theme);
        Assert.Equal("fr", settings.Language);
        Assert.True(settings.StartMinimized);
        Assert.Equal(30, settings.DefaultFps);
    }
}

public sealed class AndroidAppInfoTests
{
    [Fact]
    public void Creation_WithRequiredProperties()
    {
        var app = new AndroidAppInfo
        {
            PackageName = "com.example.app",
            AppName = "Example App",
            VersionName = "1.0.0",
            VersionCode = 1,
            IsSystemApp = false
        };

        Assert.Equal("com.example.app", app.PackageName);
        Assert.Equal("Example App", app.AppName);
        Assert.Equal("1.0.0", app.VersionName);
        Assert.Equal(1, app.VersionCode);
        Assert.False(app.IsSystemApp);
    }

    [Fact]
    public void InstalledAppsResult_CollectionsAreSettable()
    {
        var userApps = new List<AndroidAppInfo>();
        var systemApps = new List<AndroidAppInfo>();

        var result = new InstalledAppsResult
        {
            UserApps = userApps,
            SystemApps = systemApps
        };

        Assert.Empty(result.UserApps);
        Assert.Empty(result.SystemApps);
    }
}

public sealed class AndroidFileInfoTests
{
    [Fact]
    public void Creation_WithRequiredProperties()
    {
        var file = new AndroidFileInfo
        {
            FullName = "/sdcard/test.txt",
            Name = "test.txt",
            IsDirectory = false,
            Size = 1024
        };

        Assert.Equal("/sdcard/test.txt", file.FullName);
        Assert.Equal("test.txt", file.Name);
        Assert.False(file.IsDirectory);
        Assert.Equal(1024, file.Size);
    }

    [Fact]
    public void IsDirectory_CanBeTrue()
    {
        var dir = new AndroidFileInfo
        {
            FullName = "/sdcard/Documents",
            Name = "Documents",
            IsDirectory = true
        };

        Assert.True(dir.IsDirectory);
    }
}

public sealed class EnumsTests
{
    [Fact]
    public void ConnectionType_HasCorrectValues()
    {
        Assert.Equal(0, (int)ConnectionType.Usb);
        Assert.Equal(1, (int)ConnectionType.Wireless);
        Assert.Equal(2, (int)ConnectionType.Unknown);
    }

    [Fact]
    public void TransferState_HasCorrectValues()
    {
        Assert.Equal(0, (int)TransferState.Pending);
        Assert.Equal(1, (int)TransferState.InProgress);
        Assert.Equal(2, (int)TransferState.Completed);
        Assert.Equal(3, (int)TransferState.Failed);
        Assert.Equal(4, (int)TransferState.Cancelled);
    }

    [Fact]
    public void AndroidKeyType_HasAllValues()
    {
        var values = Enum.GetValues<AndroidKeyType>();
        Assert.Equal(11, values.Length);
    }

    [Fact]
    public void InputEventType_HasCorrectValues()
    {
        Assert.Equal(0, (int)InputEventType.Tap);
        Assert.Equal(5, (int)InputEventType.KeyPress);
        Assert.Equal(12, (int)InputEventType.Text);
    }

    [Fact]
    public void DeviceOrientation_HasFourValues()
    {
        var values = Enum.GetValues<DeviceOrientation>();
        Assert.Equal(4, values.Length);
    }

    [Fact]
    public void StreamQuality_HasFiveValues()
    {
        var values = Enum.GetValues<StreamQuality>();
        Assert.Equal(5, values.Length);
    }
}
