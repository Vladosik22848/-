using System;
using System.Security.Cryptography;
using System.Text;

namespace Kursovaya.Security
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16;   // 128 бит
        private const int HashSize = 32;   // 256 бит
        private const int Iterations = 100000;
        private const string Prefix = "PBKDF2";

        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Для .NET Framework используем Rfc2898DeriveBytes без HashAlgorithmName (HMACSHA1 под капотом)
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                var hash = pbkdf2.GetBytes(HashSize);
                return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
            }
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return false;

            if (stored.StartsWith(Prefix + "$", StringComparison.Ordinal))
            {
                // PBKDF2$<iter>$<saltB64>$<hashB64>
                var parts = stored.Split('$');
                if (parts.Length != 4) return false;

                if (!int.TryParse(parts[1], out var iter)) return false;
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iter))
                {
                    var actual = pbkdf2.GetBytes(expected.Length);
                    return FixedTimeEquals(actual, expected);
                }
            }

            // Совместимость со старым SHA256-hex
            return VerifySha256Legacy(password, stored);
        }

        public static string HashSha256Legacy(string password)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password ?? ""));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static bool VerifySha256Legacy(string password, string hex)
        {
            var computed = HashSha256Legacy(password);
            return string.Equals(computed, hex, StringComparison.OrdinalIgnoreCase);
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}