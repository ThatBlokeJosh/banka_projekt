using System.Security.Cryptography;

namespace Banka
{
    public class Auth
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100000;
        public static string HashPassword(string password)
        {
            byte[] salt = new byte[SaltSize];
            RandomNumberGenerator.Fill(salt);

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(HashSize);

                return Convert.ToBase64String(Combine(salt, hash));
            }
        }

        public static bool VerifyPassword(string password, string hash)
        {
            byte[] storedBytes = Convert.FromBase64String(hash);

            byte[] salt = new byte[SaltSize];
            Array.Copy(storedBytes, 0, salt, 0, SaltSize);

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] newHash = pbkdf2.GetBytes(HashSize);

                for (int i = 0; i < HashSize; i++)
                {
                    if (newHash[i] != storedBytes[SaltSize + i])
                        return false;
                }
            }
            return true;
        }
        private static byte[] Combine(byte[] salt, byte[] hash)
        {
            byte[] combined = new byte[salt.Length + hash.Length];
            Array.Copy(salt, 0, combined, 0, salt.Length);
            Array.Copy(hash, 0, combined, salt.Length, hash.Length);
            return combined;
        }
    }
}