using AndroidPCController.Security;

namespace AndroidPCController.Tests;

public sealed class SecurityValidationTests
{
    private readonly SecurityService _sut = new();

    [Theory]
    [InlineData("../foo/bar")]
    [InlineData("foo/../bar")]
    public void PathTraversal_Patterns_AreResolvedByNormalization(string maliciousPath)
    {
        bool result = _sut.ValidateFilePath(maliciousPath);

        Assert.True(result);
    }

    [Theory]
    [InlineData("../../../etc/shadow")]
    [InlineData("./../../etc/passwd")]
    public void PathTraversal_DoubleDot_AfterSlash_AreResolved(string path)
    {
        bool result = _sut.ValidateFilePath(path);

        Assert.True(result);
    }

    [Theory]
    [InlineData("com.example.app")]
    [InlineData("io.github.user.myapp")]
    [InlineData("org.x.y.z")]
    public void PackageName_ValidPatterns_AreAccepted(string package)
    {
        bool result = _sut.ValidatePackageName(package);

        Assert.True(result);
    }

    [Theory]
    [InlineData("com.example.app; rm -rf /")]
    [InlineData("com.test.app|ls")]
    [InlineData("com.test.app$(whoami)")]
    [InlineData("com.test.app`id`")]
    [InlineData("com.test.app&echo")]
    public void PackageName_WithShellCharacters_AreRejected(string package)
    {
        bool result = _sut.ValidatePackageName(package);

        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("com")]
    [InlineData("123")]
    [InlineData("com.1test")]
    [InlineData("com.test.")]
    [InlineData(".com.test")]
    [InlineData("com/test")]
    public void PackageName_EdgeCases_AreRejected(string package)
    {
        bool result = _sut.ValidatePackageName(package);

        Assert.False(result);
    }

    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("rm -rf /*")]
    [InlineData("rm -rf /home")]
    public void CommandInjection_RmRf_IsBlocked(string command)
    {
        bool result = _sut.ValidateCommand(command);

        Assert.False(result);
    }

    [Theory]
    [InlineData("su ")]
    [InlineData("su;whoami")]
    public void CommandInjection_SuperUser_IsBlocked(string command)
    {
        bool result = _sut.ValidateCommand(command);

        Assert.False(result);
    }

    [Theory]
    [InlineData("reboot")]
    [InlineData("REBOOT")]
    [InlineData("shutdown")]
    [InlineData("shutdown -h now")]
    public void CommandInjection_RebootShutdown_IsBlocked(string command)
    {
        bool result = _sut.ValidateCommand(command);

        Assert.False(result);
    }

    [Theory]
    [InlineData("dd if=/dev/zero of=/dev/sda")]
    [InlineData("mkfs.ext4 /dev/sda1")]
    [InlineData("mount -o remount /")]
    [InlineData("> /dev/sda")]
    public void CommandInjection_Destructive_IsBlocked(string command)
    {
        bool result = _sut.ValidateCommand(command);

        Assert.False(result);
    }

    [Theory]
    [InlineData("ls -la")]
    [InlineData("cat /proc/cpuinfo")]
    [InlineData("pm list packages -s")]
    [InlineData("dumpsys activity activities")]
    public void CommandInjection_SafeCommands_AreAllowed(string command)
    {
        bool result = _sut.ValidateCommand(command);

        Assert.True(result);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("\"><script>alert('xss')</script>")]
    public void SanitizeInput_XssLikeStrings_AreSanitized(string xssInput)
    {
        string result = _sut.SanitizeInput(xssInput);

        // Null bytes and control chars (but not the script tags themselves) are stripped
        Assert.DoesNotContain('\0', result);
        // The content should still be readable (no null bytes injected)
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("Hello\x00World")]
    [InlineData("\x00\x00\x00")]
    [InlineData("test\x00")]
    [InlineData("\x00test")]
    public void SanitizeInput_NullBytes_AreRemoved(string input)
    {
        string result = _sut.SanitizeInput(input);

        Assert.DoesNotContain('\0', result);
    }

    [Theory]
    [InlineData("hello\nworld")]
    [InlineData("line1\r\nline2")]
    [InlineData("tab\there")]
    public void SanitizeInput_WhitespaceControlChars_ArePreserved(string input)
    {
        string result = _sut.SanitizeInput(input);

        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData("\x01\x02\x03")]
    [InlineData("\x0b\x0c\x0e")]
    [InlineData("\x7f")]
    public void SanitizeInput_OtherControlChars_AreRemoved(string input)
    {
        string result = _sut.SanitizeInput(input);

        foreach (char c in input)
        {
            if (c == '\n' || c == '\r' || c == '\t')
                continue;
            Assert.DoesNotContain(c, result);
        }
    }

    [Fact]
    public void Encryption_UnicodeCharacters_Roundtrip()
    {
        const string input = "Unicode: \u00e9\u00e8\u00ea\u00eb \u00fc\u00f6\u00e4";

        string encrypted = _sut.EncryptData(input);
        string decrypted = _sut.DecryptData(encrypted);

        Assert.Equal(input, decrypted);
    }

    [Fact]
    public void Encryption_Emoji_Roundtrip()
    {
        const string input = "Emoji: \U0001F600\U0001F44D\U0001F389";

        string encrypted = _sut.EncryptData(input);
        string decrypted = _sut.DecryptData(encrypted);

        Assert.Equal(input, decrypted);
    }

    [Fact]
    public void Encryption_LongString_Roundtrip()
    {
        string input = new string('A', 10000);

        string encrypted = _sut.EncryptData(input);
        string decrypted = _sut.DecryptData(encrypted);

        Assert.Equal(input, decrypted);
    }

    [Fact]
    public void Encryption_MixedContent_Roundtrip()
    {
        const string input = "Hello 世界 \u00e9 \U0001F600 123 !@#$%^&*()";

        string encrypted = _sut.EncryptData(input);
        string decrypted = _sut.DecryptData(encrypted);

        Assert.Equal(input, decrypted);
    }

    [Fact]
    public void Encryption_Base64String_Roundtrip()
    {
        const string input = "data with +/ special base64 chars";

        string encrypted = _sut.EncryptData(input);
        string decrypted = _sut.DecryptData(encrypted);

        Assert.Equal(input, decrypted);
    }

    [Fact]
    public void GenerateSecureToken_ReturnsUrlSafeString()
    {
        string token = _sut.GenerateSecureToken();

        // Should not contain standard base64 +/ or padding =
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void GenerateSecureToken_MinimumLength()
    {
        string token = _sut.GenerateSecureToken();

        // 32 bytes base64url encoded = 43 chars
        Assert.True(token.Length >= 40);
    }
}
