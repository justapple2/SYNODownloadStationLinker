using System;
using System.IO;
using System.Security.Cryptography;

namespace SYNODownloadStationLinker.Models;

public class AESKey
{
    private static readonly string SavePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public string Key { get; set; } = string.Empty;
    public string IV { get; set; } = string.Empty;

    public void GetArrayBytes(out byte[] key, out byte[] iv)
    {
        if (string.IsNullOrWhiteSpace(Key) || string.IsNullOrWhiteSpace(IV))
        {
            key = [];
            iv = [];
            return;
        }

        key = Convert.FromBase64String(Key);
        iv = Convert.FromBase64String(IV);
    }

    public static AESKey Load()
    {
        var fileName = Path.Combine(SavePath, "AESKey.json");
        if (!File.Exists(fileName))
        {
            var newAes = Create();
            Save(newAes);
            return newAes;
        }

        return JsonHelper.Deserialize<AESKey>(File.ReadAllText(fileName)) ?? Create();
    }

    private static void Save(AESKey aes)
    {
        var fileName = Path.Combine(SavePath, "AESKey.json");
        var tmp = JsonHelper.Serialize(aes);
        using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
        using var sr = new StreamWriter(fs);
        sr.Write(tmp);
    }

    private static AESKey Create()
    {
        byte[] key = new byte[32];
        byte[] iv = new byte[16];
        RandomNumberGenerator.Fill(key);
        RandomNumberGenerator.Fill(iv);

        return new AESKey
        {
            Key = Convert.ToBase64String(key),
            IV = Convert.ToBase64String(iv)
        };
    }
}

