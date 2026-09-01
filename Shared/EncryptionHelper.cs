// Created on Jul 17, 2026 @ 09:00:00 -> Shared RSA encryption utilities
using System;
using System.Security.Cryptography;
using System.Text;

namespace Lyracist.Shared
{
    public static class EncryptionHelper
    {
        private static readonly byte[] Entropy = { 0x4C, 0x79, 0x72, 0x61, 0x63, 0x69, 0x73, 0x74 }; // "Lyracist" in hex

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                // This file is linked into apps that don't all link Globals.cs (e.g. LyracistKeyGen),
                // so it can't depend on Globals.LogError - Trace.TraceError still isn't Debug-only,
                // unlike Debug.WriteLine.
                System.Diagnostics.Trace.TraceError($"DPAPI Encryption failed: {ex}");
                return plainText; // Fallback
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return string.Empty;
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] plainBytes = ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                // If it fails to decrypt, it might already be in plaintext (not yet encrypted/migrated)
                return cipherText;
            }
        }
    }
}
