using System;
using System.Security.Cryptography;
using System.Text;

public static class CryptoUtil
{
    public static byte[] GenerateRandomBytes(int length)
    {
        var b = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(b);
        return b;
    }

    public static string ToBase64(byte[] data) => Convert.ToBase64String(data);
    public static byte[] FromBase64(string s) => Convert.FromBase64String(s);

    // RSA encrypt (public key in XML or PEM; example uses RSAParameters or RSACryptoServiceProvider)
    public static byte[] RsaEncrypt(byte[] data, RSAParameters pubParams)
    {
        using (var rsa = RSA.Create())
        {
            rsa.ImportParameters(pubParams);
            return rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
        }
    }

    public static byte[] RsaDecrypt(byte[] cipher, RSAParameters privParams)
    {
        using (var rsa = RSA.Create())
        {
            rsa.ImportParameters(privParams);
            return rsa.Decrypt(cipher, RSAEncryptionPadding.Pkcs1);
        }
    }

    // AES encrypt with key + random IV; returns IV + ciphertext
    public static byte[] AesEncrypt(byte[] plain, byte[] key)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var encryptor = aes.CreateEncryptor())
            using (var ms = new System.IO.MemoryStream())
            {
                // prepend IV
                ms.Write(aes.IV, 0, aes.IV.Length);
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(plain, 0, plain.Length);
                    cs.FlushFinalBlock();
                }
                return ms.ToArray();
            }
        }
    }

    public static byte[] AesDecrypt(byte[] ivPlusCipher, byte[] key)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var iv = new byte[aes.BlockSize / 8];
            Array.Copy(ivPlusCipher, 0, iv, 0, iv.Length);
            var cipher = new byte[ivPlusCipher.Length - iv.Length];
            Array.Copy(ivPlusCipher, iv.Length, cipher, 0, cipher.Length);

            aes.IV = iv;
            using (var decryptor = aes.CreateDecryptor())
            using (var ms = new System.IO.MemoryStream(cipher))
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (var sr = new System.IO.MemoryStream())
            {
                cs.CopyTo(sr);
                return sr.ToArray();
            }
        }
    }
}
