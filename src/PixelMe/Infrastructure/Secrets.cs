using System.Security.Cryptography;
using System.Text;

namespace PixelMe.Infrastructure;

public static class Secrets
{
    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain))
        {
            return string.Empty;
        }

        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    public static string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return string.Empty;
        }

        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(stored), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            Log.Warn("A stored secret could not be read", ex);
            return string.Empty;
        }
    }
}
