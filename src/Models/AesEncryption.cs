using System;
using System.IO;
using System.Security.Cryptography;

namespace SYNODownloadStationLinker.Models;
public class AesEncryption
{
    public static string Encrypt2String(string plainText, byte[] key, byte[] iv)
    {
        byte[] encryptedBytes = EncryptString(plainText, key, iv);
        return Convert.ToBase64String(encryptedBytes);
    }

    private static byte[] EncryptString(string plainText, byte[] key, byte[] iv)
    {
        if (string.IsNullOrEmpty(plainText))
            return [];

        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.IV = iv;

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                }

                return msEncrypt.ToArray();
            }
        }
    }

    public static string Decrypt4String(string cipherText, byte[] key, byte[] iv)
    {
        byte[] encryptedBytes = Convert.FromBase64String(cipherText);
        return DecryptBytes(encryptedBytes, key, iv);
    }

    private static string DecryptBytes(byte[] cipherText, byte[] key, byte[] iv)
    {
        if (cipherText == null || cipherText.Length == 0)
            return null;

        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.IV = iv;

            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            using (MemoryStream msDecrypt = new MemoryStream(cipherText))
            {
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
        }
    }
   
}