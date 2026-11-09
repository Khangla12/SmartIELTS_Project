using System.Security.Cryptography;

namespace SmartIELTS.Domain.Common;

/// <summary>
/// Băm mật khẩu bằng PBKDF2-SHA256 (100.000 vòng lặp, salt ngẫu nhiên 16 byte).
/// Định dạng lưu: {iterations}.{saltBase64}.{hashBase64}
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public static string Hash(string password)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Mật khẩu không được để trống", nameof(password));

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Kiểm tra mật khẩu. Nếu <paramref name="storedHash"/> không đúng định dạng PBKDF2
    /// (dữ liệu seed cũ lưu plaintext) thì vẫn cho phép so sánh trực tiếp để không khóa tài khoản cũ.
    /// </summary>
    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash)) return false;

        var parts = storedHash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out int iterations))
        {
            return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(password),
                System.Text.Encoding.UTF8.GetBytes(storedHash));
        }

        try
        {
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] expectedHash = Convert.FromBase64String(parts[2]);
            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Trả về true nếu chuỗi đã ở định dạng băm PBKDF2 (dùng để nâng cấp dữ liệu cũ).</summary>
    public static bool IsHashed(string storedHash)
        => !string.IsNullOrEmpty(storedHash) && storedHash.Split('.').Length == 3;
}
