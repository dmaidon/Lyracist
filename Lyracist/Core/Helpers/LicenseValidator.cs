using System;
using System.Security.Cryptography;
using System.Text;

namespace Lyracist.Core.Helpers
{
    public static class LicenseValidator
    {
        private const string SecretSalt = "PrudenceGodBeWithUsSimplicitySmith2026";
        private const string Base32Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Omit easily-confused: 0, 1, I, O

        /// <summary>
        /// Generates a cryptographically valid 15-character serial key in format XXXXX-XXXXX-XXXXX.
        /// </summary>
        public static string GenerateKey(string firstName, string lastName, string stageName, string email)
        {
            string fName = (firstName ?? "").Trim().ToLower();
            string lName = (lastName ?? "").Trim().ToLower();
            string sName = (stageName ?? "").Trim().ToLower();
            if (string.IsNullOrEmpty(sName) || sName.Equals("none", StringComparison.OrdinalIgnoreCase)) sName = "none";
            string mail = (email ?? "").Trim().ToLower();

            string rawInput = $"{fName}:{lName}:{sName}:{mail}:{SecretSalt}";

            using var sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawInput));

            var sb = new StringBuilder();
            for (int i = 0; i < 15; i++)
            {
                // Take 2 bytes per character for sufficient range & entropy
                int val = (hashBytes[i * 2] << 8 | hashBytes[i * 2 + 1]) % Base32Chars.Length;
                sb.Append(Base32Chars[val]);
            }

            string rawKey = sb.ToString();
            return $"{rawKey[0..5]}-{rawKey[5..10]}-{rawKey[10..15]}";
        }

        /// <summary>
        /// Validates if the key matches the provided details.
        /// </summary>
        public static bool ValidateKey(string firstName, string lastName, string stageName, string email, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            string generated = GenerateKey(firstName, lastName, stageName, email);
            return string.Equals(generated, key.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
