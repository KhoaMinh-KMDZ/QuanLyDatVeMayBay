using Microsoft.AspNetCore.Identity;

namespace QL_DatVeMayBay.Data;

internal static class PasswordStorage
{
    private const string Prefix = "PBKDF2:";
    private static readonly PasswordHasher<string> Hasher = new();

    public static bool IsHashed(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Hash(string userId, string password) =>
        Prefix + Hasher.HashPassword(userId, password);

    public static bool Verify(string userId, string value, string password) =>
        IsHashed(value)
            ? Hasher.VerifyHashedPassword(userId, value[Prefix.Length..], password) != PasswordVerificationResult.Failed
            : value == password;
}
