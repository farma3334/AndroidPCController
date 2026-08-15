namespace AndroidPCController.Core.Interfaces;

public interface ISecurityService
{
    string EncryptData(string plainText);
    string DecryptData(string encryptedText);
    bool ValidateFilePath(string path);
    bool ValidatePackageName(string packageName);
    bool ValidateCommand(string command);
    string SanitizeInput(string input);
    string GenerateSecureToken();
    bool VerifyDeviceIdentity(string serial, string expectedHash);
}
