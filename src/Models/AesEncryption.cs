using System;
using System.IO;
using System.Security.Cryptography;

namespace SYNODownloadStationLinker.Models;

public static class AesEncryption
{
    public static string Encrypt2String(string plainText, byte[] key, byte[] iv)
    {
        byte[] encryptedBytes = EncryptString(plainText, key, iv);
        return Convert.ToBase64String(encryptedBytes);
    }

    public static string Decrypt4String(string cipherText, byte[] key, byte[] iv)
    {
        byte[] encryptedBytes = Convert.FromBase64String(cipherText);
        return DecryptBytes(encryptedBytes, key, iv);
    }

    private static byte[] EncryptString(string plainText, byte[] key, byte[] iv)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return [];
        }

        using var aesAlg = Aes.Create();
        aesAlg.Key = key;
        aesAlg.IV = iv;

        using var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
        using var msEncrypt = new MemoryStream();
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }

        return msEncrypt.ToArray();
    }

    private static string DecryptBytes(byte[] cipherText, byte[] key, byte[] iv)
    {
        if (cipherText.Length == 0)
        {
            return string.Empty;
        }

        using var aesAlg = Aes.Create();
        aesAlg.Key = key;
        aesAlg.IV = iv;

        using var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
        using var msDecrypt = new MemoryStream(cipherText);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);
        return srDecrypt.ReadToEnd();
    }
}

