using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AndroidPCController.Core.Interfaces;

namespace AndroidPCController.Security;

public sealed partial class SecurityService : ISecurityService
{
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("AndroidPCController_v1_Salt_2026!");
    private const int KeySize = 32;
    private const int IvSize = 16;
    private const int Iterations = 100_000;

    public string EncryptData(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        using var deriveBytes = new Rfc2898DeriveBytes(
            GetMachineKey(), Salt, Iterations, HashAlgorithmName.SHA256);
        var key = deriveBytes.GetBytes(KeySize);
        var iv = deriveBytes.GetBytes(IvSize);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(encrypted);
    }

    public string DecryptData(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText)) return string.Empty;

        using var deriveBytes = new Rfc2898DeriveBytes(
            GetMachineKey(), Salt, Iterations, HashAlgorithmName.SHA256);
        var key = deriveBytes.GetBytes(KeySize);
        var iv = deriveBytes.GetBytes(IvSize);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var encryptedBytes = Convert.FromBase64String(encryptedText);
        var decrypted = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
        return Encoding.UTF8.GetString(decrypted);
    }

    public bool ValidateFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var normalized = Path.GetFullPath(path);
        if (normalized.Contains("..", StringComparison.Ordinal)) return false;

        var invalidChars = Path.GetInvalidPathChars();
        if (path.Any(c => invalidChars.Contains(c))) return false;

        return true;
    }

    public bool ValidatePackageName(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName)) return false;
        return PackageNameRegex().IsMatch(packageName);
    }

    public bool ValidateCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;

        var dangerous = new[]
        {
            "rm -rf /", "rm -rf /*", "reboot", "shutdown",
            "su ", "su;", "mount -o remount", "dd if=",
            "mkfs", "> /dev/", ":(){ :|:& };:"
        };

        var lower = command.ToLowerInvariant();
        return !dangerous.Any(d => lower.Contains(d, StringComparison.Ordinal));
    }

    public string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c == '\0') continue;
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t') continue;
            sb.Append(c);
        }
        return sb.ToString();
    }

    public string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public bool VerifyDeviceIdentity(string serial, string expectedHash)
    {
        if (string.IsNullOrEmpty(serial) || string.IsNullOrEmpty(expectedHash)) return false;

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(serial + Convert.ToBase64String(Salt)));
        var actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actualHash),
            Encoding.UTF8.GetBytes(expectedHash.ToLowerInvariant()));
    }

    private static string GetMachineKey()
    {
        var machineName = Environment.MachineName;
        var userName = Environment.UserName;
        return $"APC_{machineName}_{userName}";
    }

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$")]
    private static partial Regex PackageNameRegex();
}
