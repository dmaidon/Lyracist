// Edited on Jul 17, 2026 @ 09:00:00 -> RSA key signing and management
using System;
using System.Security.Cryptography;
using System.Text;

namespace LyracistKeyGen
{
    public static class LicenseManager
    {
        private const string PrivateKeyXml = @"<RSAKeyValue><Modulus>yNiIy7PlY5E+t5x1vy7xL7dVSc4IiuCbQSAEH6CdDnkPaZRniMRSPnTTBoIk2VU4uI08OJT0xWPswZB8krolhaDIihMsRNCqZvqfBZo2d/b2mp/BxVsJpGmQzbVK4CVJitEp38Om0QpZg6TZDRLkJoImTu129mh/JX+0UvTVwS0=</Modulus><Exponent>AQAB</Exponent><P>3V9G8OhBMX0vJQXdrpgruLwWFTpBgHWN+8bl+eQeX654MZXq/k6OxARirVqS1gVmkKRErKwUx4JOy1Z8qtXUbw==</P><Q>6ENKq3dcwZExBWWEPo96QGgMhUECUK0QF4bIXWIiqrPi/yTZlrT8Cz+KtcE8apL8XLu8/HundS4EI6pKofiqIw==</Q><DP>pC+JLx4jVDAzqjLqkxbbvp0Jh974O+10TBvd7/QoLvD4xlYZv1nGe02BXm+B3miNBJRBNww+MSbNh/RybEZB0w==</DP><DQ>ZYK7iNNDO+pcFXK36KvGj42qIzc1btMknFOxEHdKlXbHeCG/44k4OyZLVoKdCCszlsgKogLdPm6dKoVL1xyaJw==</DQ><InverseQ>0x4x+D2tQ6xVutL5/7G5kOpEjY7HgX3ak8mEqgFJkxMHJ2g3ykDt2pa2m0zjhpqNwy5O7HIfWbpbIsHJCT1UKQ==</InverseQ><D>NlOT1P3JG4CLJWE13EvXQ1/kuvz3BJGyjRAa7W8lbGfEintw8eaglHJHLmh/jSXnHMxfMLLh7o6T2Nu7RnkBcsQRZj7EJpKlBJbGkQxbiOOOTZantpxVmDo3F8H3oA3H4PMVepXH5lRPB4CqrpE2myWf6gCDKan13LD1T95YcGU=</D></RSAKeyValue>";

        /// <summary>
        /// Generates a cryptographically valid asymmetric RSA signature of the registration parameters as the serial key.
        /// </summary>
        public static string GenerateKey(string firstName, string lastName, string stageName, string email)
        {
            string fName = (firstName ?? "").Trim().ToLower();
            string lName = (lastName ?? "").Trim().ToLower();
            string sName = (stageName ?? "").Trim().ToLower();
            if (string.IsNullOrEmpty(sName) || sName.Equals("none", StringComparison.OrdinalIgnoreCase)) sName = "none";
            string mail = (email ?? "").Trim().ToLower();

            string payload = $"{fName}:{lName}:{sName}:{mail}";
            byte[] dataToSign = Encoding.UTF8.GetBytes(payload);

            using var rsa = RSA.Create();
            rsa.FromXmlString(PrivateKeyXml);

            byte[] signatureBytes = rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signatureBytes);
        }

        /// <summary>
        /// Validates if the key matches the provided details.
        /// </summary>
        public static bool ValidateKey(string firstName, string lastName, string stageName, string email, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            try
            {
                string fName = (firstName ?? "").Trim().ToLower();
                string lName = (lastName ?? "").Trim().ToLower();
                string sName = (stageName ?? "").Trim().ToLower();
                if (string.IsNullOrEmpty(sName) || sName.Equals("none", StringComparison.OrdinalIgnoreCase)) sName = "none";
                string mail = (email ?? "").Trim().ToLower();

                string payload = $"{fName}:{lName}:{sName}:{mail}";
                byte[] dataToVerify = Encoding.UTF8.GetBytes(payload);
                byte[] signatureBytes = Convert.FromBase64String(key.Trim());

                using var rsa = RSA.Create();
                rsa.FromXmlString(PrivateKeyXml);

                return rsa.VerifyData(dataToVerify, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }
    }
}