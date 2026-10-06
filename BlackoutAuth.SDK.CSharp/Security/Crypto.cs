using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BlackoutAuth.SDK.CSharp.Security
{
    internal static class Crypto
    {
        public static string DecryptAes256(string cipherTextBase64, string keyHex)
        {
            if (string.IsNullOrEmpty(cipherTextBase64)) return "";

            byte[] fullCipher = Convert.FromBase64String(cipherTextBase64);
            if (fullCipher.Length < 16) return "";

            byte[] key;
            using (var sha256 = SHA256.Create())
            {
                key = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyHex));
            }

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
    }
}
