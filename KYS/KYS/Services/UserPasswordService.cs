using System.Security.Cryptography;
using System.Text;
using KYS.Models;
using Microsoft.AspNetCore.Identity;

namespace KYS.Services;

public sealed class UserPasswordService(IPasswordHasher<User01> hasher)
{
    private const string Prefix = "KYS$";

    public string Hash(User01 user, string password) => Prefix + hasher.HashPassword(user, password);

    public bool Verify(User01 user, string password, out bool needsUpgrade)
    {
        needsUpgrade = false;
        if (string.IsNullOrEmpty(user.Password)) return false;

        if (user.Password.StartsWith(Prefix, StringComparison.Ordinal))
        {
            PasswordVerificationResult result;
            try
            {
                result = hasher.VerifyHashedPassword(user, user.Password[Prefix.Length..], password);
            }
            catch (FormatException)
            {
                return false;
            }

            needsUpgrade = result == PasswordVerificationResult.SuccessRehashNeeded;
            return result != PasswordVerificationResult.Failed;
        }

        // Existing user01 records remain usable; upgrade them after a successful login.
        var stored = SHA256.HashData(Encoding.UTF8.GetBytes(user.Password));
        var supplied = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        needsUpgrade = CryptographicOperations.FixedTimeEquals(stored, supplied);
        return needsUpgrade;
    }
}
