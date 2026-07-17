// Edited on Jul 17, 2026 @ 09:00:00 -> RSA license validation
using System;
using System.Security.Cryptography;
using System.Text;

namespace Lyracist.Core.Helpers
{
    public static class LicenseValidator
    {
        private const string PublicKeyXml = @"<RSAKeyValue><Modulus>yNiIy7PlY5E+t5x1vy7xL7dVSc4IiuCbQSAEH6CdDnkPaZRniMRSPnTTBoIk2VU4uI08OJT0xWPswZB8krolhaDIihMsRNCqZvqfBZo2d/b2mp/BxVsJpGmQzbVK4CVJitEp38Om0QpZg6TZDRLkJoImTu129mh/JX+0UvTVwS0=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        /// <summary>
        /// Validates if the key is a cryptographically valid RSA signature of the registration parameters.
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
                rsa.FromXmlString(PublicKeyXml);

                return rsa.VerifyData(dataToVerify, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }
    }
}
