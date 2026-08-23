using System.Security.Cryptography;
using System.Text;
using VoicePin.Core.Services;

namespace VoicePin.Infrastructure.Security;

/// <summary>Windows DPAPI(현재 사용자 범위)로 문자열을 보호/복원한다. Deepgram API 키 저장에 사용.</summary>
public class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VoicePin.v1.secret");

    public string Protect(string plainText)
    {
        var plain = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedText)
    {
        var protectedBytes = Convert.FromBase64String(protectedText);
        var plain = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plain);
    }
}
