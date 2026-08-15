using AndroidPCController.Core.Interfaces;
using AndroidPCController.Infrastructure;

namespace AndroidPCController.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsService _sut;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"SettingsTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Create a SettingsService that uses our temp directory
        _sut = new SettingsService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    [Fact]
    public void Get_ReturnsDefault_ForMissingKey()
    {
        string result = _sut.Get("nonexistent.key", "default");

        Assert.Equal("default", result);
    }

    [Fact]
    public void Get_ReturnsDefault_WhenNoValueSet()
    {
        int result = _sut.Get("missing.int", 42);

        Assert.Equal(42, result);
    }

    [Fact]
    public void Get_ReturnsDefault_ForMissingBool()
    {
        bool result = _sut.Get("missing.bool", true);

        Assert.True(result);
    }

    [Fact]
    public void SetGet_Roundtrip_String()
    {
        _sut.Set("test.string", "hello");

        string result = _sut.Get("test.string", "default");

        Assert.Equal("hello", result);
    }

    [Fact]
    public void SetGet_Roundtrip_Int()
    {
        _sut.Set("test.int", 123);

        int result = _sut.Get("test.int", 0);

        Assert.Equal(123, result);
    }

    [Fact]
    public void SetGet_Roundtrip_Bool()
    {
        _sut.Set("test.bool", true);

        bool result = _sut.Get("test.bool", false);

        Assert.True(result);
    }

    [Fact]
    public void SetGet_Roundtrip_Double()
    {
        _sut.Set("test.double", 3.14);

        double result = _sut.Get("test.double", 0.0);

        Assert.Equal(3.14, result, 2);
    }

    [Fact]
    public void SetGet_Roundtrip_Long()
    {
        _sut.Set("test.long", 9_999_999_999L);

        long result = _sut.Get("test.long", 0L);

        Assert.Equal(9_999_999_999L, result);
    }

    [Fact]
    public void SettingChanged_Event_Fires()
    {
        SettingChangedEventArgs? eventArgs = null;
        _sut.SettingChanged += (_, e) => eventArgs = e;

        _sut.Set("test.event", "value");

        Assert.NotNull(eventArgs);
        Assert.Equal("test.event", eventArgs!.Key);
        Assert.Equal("value", eventArgs.Value);
    }

    [Fact]
    public void SettingChanged_Event_FiresCorrectValue()
    {
        SettingChangedEventArgs? eventArgs = null;
        _sut.SettingChanged += (_, e) => eventArgs = e;

        _sut.Set("numeric.key", 42);

        Assert.Equal(42, eventArgs!.Value);
    }

    [Fact]
    public void Set_OverwritesPreviousValue()
    {
        _sut.Set("test.overwrite", "first");
        _sut.Set("test.overwrite", "second");

        string result = _sut.Get("test.overwrite", "");

        Assert.Equal("second", result);
    }

    [Fact]
    public void SettingChanged_DoesNotFire_OnGet()
    {
        int fireCount = 0;
        _sut.SettingChanged += (_, _) => fireCount++;

        _sut.Get("any.key", "default");

        Assert.Equal(0, fireCount);
    }

    [Fact]
    public void SetGet_Roundtrip_ComplexObject()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        _sut.Set("test.dict", dict);

        var result = _sut.Get<Dictionary<string, int>>("test.dict", new Dictionary<string, int>());

        Assert.Equal(1, result["a"]);
        Assert.Equal(2, result["b"]);
    }

    [Fact]
    public void SetGet_Roundtrip_EmptyString()
    {
        _sut.Set("test.empty", "");

        string result = _sut.Get("test.empty", "default");

        Assert.Equal("", result);
    }

    [Fact]
    public void SetGet_DifferentKeys_Independent()
    {
        _sut.Set("key.one", "value1");
        _sut.Set("key.two", "value2");

        string r1 = _sut.Get("key.one", "");
        string r2 = _sut.Get("key.two", "");

        Assert.Equal("value1", r1);
        Assert.Equal("value2", r2);
    }

    [Fact]
    public void SettingChanged_Event_FiresMultipleTimes()
    {
        int fireCount = 0;
        _sut.SettingChanged += (_, _) => fireCount++;

        _sut.Set("key1", "a");
        _sut.Set("key2", "b");
        _sut.Set("key3", "c");

        Assert.Equal(3, fireCount);
    }
}
