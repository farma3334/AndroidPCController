using System.Text;
using AndroidPCController.Security;

namespace AndroidPCController.Tests;

public sealed class SecurityServiceTests
{
    private readonly SecurityService _sut = new();

    [Fact]
    public void EncryptData_DecryptData_Roundtrip_ReturnsOriginal()
    {
        const string plainText = "Hello, World! This is a test string.";

        string encrypted = _sut.EncryptData(plainText);
        string decrypted = _sut.DecryptData(encrypted);

        Assert.NotEqual(plainText, encrypted);
        Assert.Equal(plainText, decrypted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EncryptData_EmptyOrNullOrEmpty_ReturnsEmpty(string? input)
    {
        string result = _sut.EncryptData(input ?? string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DecryptData_EmptyOrNullOrEmpty_ReturnsEmpty(string? input)
    {
        string result = _sut.DecryptData(input ?? string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void EncryptData_SpecialCharacters_Roundtrip()
    {
        const string plainText = "Unicode: \u00e9\u00e8\u00ea \u2603 Emoji: \U0001F600";

        string encrypted = _sut.EncryptData(plainText);
        string decrypted = _sut.DecryptData(encrypted);

        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void EncryptData_DifferentInputs_ProduceDifferentCiphertexts()
    {
        string enc1 = _sut.EncryptData("input1");
        string enc2 = _sut.EncryptData("input2");

        Assert.NotEqual(enc1, enc2);
    }

    [Fact]
    public void EncryptData_NullBytes_ProduceDifferentCiphertexts()
    {
        string enc1 = _sut.EncryptData("test\u0000value");
        string enc2 = _sut.EncryptData("testvalue");

        Assert.NotEqual(enc1, enc2);
    }

    [Theory]
    [InlineData("/sdcard/Documents/file.txt")]
    [InlineData("C:\\Users\\test\\file.txt")]
    [InlineData("relative/path/file.txt")]
    [InlineData("/data/local/tmp/test.apk")]
    public void ValidateFilePath_ValidPaths_ReturnsTrue(string path)
    {
        bool result = _sut.ValidateFilePath(path);

        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateFilePath_NullEmptyWhitespace_ReturnsFalse(string? path)
    {
        bool result = _sut.ValidateFilePath(path ?? string.Empty);

        Assert.False(result);
    }

    [Theory]
    [InlineData("../foo/bar")]
    [InlineData("foo/../bar")]
    public void ValidateFilePath_RelativeTraversal_ReturnsTrueAfterNormalization(string path)
    {
        bool result = _sut.ValidateFilePath(path);

        Assert.True(result);
    }

    [Theory]
    [InlineData("com.example.myapp")]
    [InlineData("org.apache.cordova")]
    [InlineData("io.flutter.plugins.firebasemessaging")]
    [InlineData("com.android.chrome")]
    [InlineData("a.b")]
    public void ValidatePackageName_ValidNames_ReturnsTrue(string packageName)
    {
        bool result = _sut.ValidatePackageName(packageName);

        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("com")]
    [InlineData("com.")]
    [InlineData("1com.example")]
    [InlineData("com.1example.app")]
    [InlineData(".com.example")]
    [InlineData("com/example/app")]
    [InlineData("com example app")]
    public void ValidatePackageName_InvalidNames_ReturnsFalse(string? packageName)
    {
        bool result = _sut.ValidatePackageName(packageName ?? string.Empty);

        Assert.False(result);
    }

    [Theory]
    [InlineData("ls -la")]
    [InlineData("pm list packages")]
    [InlineData("dumpsys battery")]
    [InlineData("cat /proc/cpuinfo")]
    [InlineData("getprop ro.build.version.sdk")]
    [InlineData("am start -n com.example/.MainActivity")]
    [InlineData("input tap 100 200")]
    public void ValidateCommand_SafeCommands_ReturnsTrue(string command)
    {
        bool result = _sut.ValidateCommand(command);

        Assert.True(result);
    }

    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("rm -rf /*")]
    [InlineData("reboot")]
    [InlineData("shutdown")]
    [InlineData("su ")]
    [InlineData("su;")]
    [InlineData("mount -o remount")]
    [InlineData("dd if=/dev/zero")]
    [InlineData("mkfs.ext4 /dev/sda")]
    [InlineData("> /dev/sda")]
    public void ValidateCommand_DangerousCommands_ReturnsFalse(string command)
    {
        bool result = _sut.ValidateCommand(command);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ValidateCommand_EmptyOrNull_ReturnsFalse(string? command)
    {
        bool result = _sut.ValidateCommand(command ?? string.Empty);

        Assert.False(result);
    }

    [Fact]
    public void ValidateCommand_CaseInsensitive_DetectsDangerous()
    {
        bool result = _sut.ValidateCommand("RM -RF /");

        Assert.False(result);
    }

    [Theory]
    [InlineData("hello world", "hello world")]
    [InlineData("test\x00value", "testvalue")]
    [InlineData("\u0001\u0002\u0003abc", "abc")]
    [InlineData("\u0001abc", "abc")]
    public void SanitizeInput_RemovesControlChars(string input, string expected)
    {
        string result = _sut.SanitizeInput(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeInput_PreservesNewlinesTabsCarriageReturns()
    {
        const string input = "line1\nline2\rline3\ttabbed";

        string result = _sut.SanitizeInput(input);

        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SanitizeInput_EmptyOrNull_ReturnsEmpty(string? input)
    {
        string result = _sut.SanitizeInput(input ?? string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SanitizeInput_RemovesNullBytes()
    {
        var chars = new[] { 'b', 'e', 'f', 'o', 'r', 'e', '\0', 'a', 'f', 't', 'e', 'r' };
        string input = new string(chars);

        string result = _sut.SanitizeInput(input);

        Assert.Equal("beforeafter", result);
    }

    [Fact]
    public void GenerateSecureToken_ReturnsNonEmptyString()
    {
        string token = _sut.GenerateSecureToken();

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void GenerateSecureToken_DifferentCallsReturnDifferentTokens()
    {
        string token1 = _sut.GenerateSecureToken();
        string token2 = _sut.GenerateSecureToken();

        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void GenerateSecureToken_ContainsOnlyUrlSafeCharacters()
    {
        string token = _sut.GenerateSecureToken();

        Assert.Matches("^[A-Za-z0-9_-]+$", token);
    }

    [Fact]
    public void VerifyDeviceIdentity_MatchingHash_ReturnsTrue()
    {
        const string serial = "ABCDEF123456";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(serial + Convert.ToBase64String(Encoding.UTF8.GetBytes("AndroidPCController_v1_Salt_2026!"))));
        string expectedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        bool result = _sut.VerifyDeviceIdentity(serial, expectedHash);

        Assert.True(result);
    }

    [Fact]
    public void VerifyDeviceIdentity_WrongHash_ReturnsFalse()
    {
        const string serial = "ABCDEF123456";
        const string wrongHash = "0000000000000000000000000000000000000000000000000000000000000000";

        bool result = _sut.VerifyDeviceIdentity(serial, wrongHash);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("serial", "")]
    public void VerifyDeviceIdentity_EmptyInputs_ReturnsFalse(string? serial, string hash)
    {
        bool result = _sut.VerifyDeviceIdentity(serial ?? string.Empty, hash);

        Assert.False(result);
    }

    [Fact]
    public void VerifyDeviceIdentity_CaseInsensitiveHash_ReturnsTrue()
    {
        const string serial = "TEST123";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(serial + Convert.ToBase64String(Encoding.UTF8.GetBytes("AndroidPCController_v1_Salt_2026!"))));
        string hashLower = Convert.ToHexString(hashBytes).ToLowerInvariant();
        string hashUpper = Convert.ToHexString(hashBytes).ToUpperInvariant();

        bool resultLower = _sut.VerifyDeviceIdentity(serial, hashLower);
        bool resultUpper = _sut.VerifyDeviceIdentity(serial, hashUpper);

        Assert.True(resultLower);
        Assert.True(resultUpper);
    }
}
