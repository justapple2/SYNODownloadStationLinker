using System;
using System.IO;
using System.Security.Cryptography;

namespace SYNODownloadStationLinker.Models;

public class AESKey
{
    private static string savePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public string Key { get; set; }

    public string IV { get; set; }

    public void GetArrayBytes(out byte[] key, out byte[] iv)
    {
        if (string.IsNullOrWhiteSpace(Key) || string.IsNullOrWhiteSpace(IV))
        {
            key = [];
            iv = [];
        }

        key = Convert.FromBase64String(Key);
        iv = Convert.FromBase64String(IV);
    }


    public static AESKey Load()
    {
        var fileName = Path.Combine(savePath, "AESKey.json");
        if (!File.Exists(fileName))
        {
            var newAes = Create();
            Save(newAes);
            return newAes;
        }

        var tmp = JsonHelper.Deserialize<AESKey>(File.ReadAllText(fileName));
        return tmp;
    }

    private static void Save(AESKey aes)
    {
        var fileName = Path.Combine(savePath, "AESKey.json");
        var tmp = JsonHelper.Serialize(aes);
        using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
        using var sr = new StreamWriter(fs);
        sr.Write(tmp);
    }

    private static AESKey Create()
    {
        // 生成密钥和IV
        byte[] key = new byte[32]; // 256位密钥
        byte[] iv = new byte[16]; // 128位IV
        using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(key);
            rng.GetBytes(iv);
        }

        var kStr = Convert.ToBase64String(key);
        var ivStr = Convert.ToBase64String(iv);
        return new AESKey() { Key = kStr, IV = ivStr };
    }
}