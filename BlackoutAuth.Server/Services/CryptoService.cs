using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BlackoutAuth.Server.Services
{
    public class CryptoService
    {
        public static string GenerateRandomKey(int length = 32)
        {
            byte[] bytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public static string GenerateLicenseKey(string prefix = "BLACKOUT", int parts = 4, int partLength = 4)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var result = new StringBuilder();
            if (!string.IsNullOrEmpty(prefix))
            {
                result.Append(prefix.Trim().ToUpperInvariant()).Append("-");
            }

            for (int i = 0; i < parts; i++)
            {
                for (int j = 0; j < partLength; j++)
                {
                    int index = RandomNumberGenerator.GetInt32(chars.Length);
                    result.Append(chars[index]);
                }
                if (i < parts - 1)
                    result.Append("-");
            }
            return result.ToString();
        }

        public static string ComputeHmacSha256(string message, string secret)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            using var hmac = new HMACSHA256(keyBytes);
            byte[] hash = hmac.ComputeHash(messageBytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public static string EncryptAes256(string plainText, string keyHex)
        {
            if (string.IsNullOrEmpty(plainText)) return "";

            byte[] key = Ensure32Bytes(keyHex);
            byte[] iv = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            ms.Write(iv, 0, iv.Length); // prepend IV

            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs, Encoding.UTF8))
            {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public static string DecryptAes256(string cipherTextBase64, string keyHex)
        {
            if (string.IsNullOrEmpty(cipherTextBase64)) return "";

            byte[] fullCipher = Convert.FromBase64String(cipherTextBase64);
            if (fullCipher.Length < 16) return "";

            byte[] key = Ensure32Bytes(keyHex);
            byte[] iv = new byte[16];
            Buffer.BlockCopy(fullCipher, 0, iv, 0, 16);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(fullCipher, 16, fullCipher.Length - 16);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);

            return sr.ReadToEnd();
        }

        private static byte[] Ensure32Bytes(string input)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        }
    }
}
