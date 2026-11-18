using System.Security.Cryptography;
using System.Text;

public static class TokenHelper
{
    public static byte[]? GetTokenHash(HttpRequest request)
    {
        var authHeader = request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return null;

        var rawToken = authHeader.Substring("Bearer ".Length).Trim();
        return SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
    }
}