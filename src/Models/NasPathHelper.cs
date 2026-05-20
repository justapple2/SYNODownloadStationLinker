using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SYNODownloadStationLinker.Models;

public static class NasPathHelper
{
    public static NasPathResult EnsureDirectory(
        string nasUrl,
        string downloadPath,
        string username = "",
        string password = "")
    {
        if (string.IsNullOrWhiteSpace(downloadPath))
        {
            return new NasPathResult(true, null, "未设置下载目录，跳过目录预创建。");
        }

        if (!TryGetHost(nasUrl, out var host))
        {
            return new NasPathResult(false, null, "NAS 地址无效，无法解析 SMB 主机名。");
        }

        var segments = downloadPath
            .Replace('/', '\\')
            .Trim('\\')
            .Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            return new NasPathResult(true, null, "下载目录为空，跳过目录预创建。");
        }

        var shareRoot = $@"\\{host}\{segments[0]}";
        var uncPath = segments.Length == 1
            ? shareRoot
            : $@"{shareRoot}\{string.Join('\\', segments.Skip(1))}";

        try
        {
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                var connectResult = ConnectToNasShare(shareRoot, username, password);
                if (!connectResult.Success && !Directory.Exists(uncPath))
                {
                    return new NasPathResult(false, uncPath, connectResult.Message);
                }
            }

            Directory.CreateDirectory(uncPath);
            return new NasPathResult(true, uncPath, $"目录已就绪：{uncPath}");
        }
        catch (Exception ex)
        {
            return new NasPathResult(false, uncPath, $"创建目录失败：{ex.Message}");
        }
    }

    private static bool TryGetHost(string nasUrl, out string host)
    {
        host = string.Empty;

        if (Uri.TryCreate(nasUrl, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            host = uri.Host;
            return true;
        }

        var withoutSlashes = nasUrl.Trim().Trim('\\', '/');
        if (!string.IsNullOrWhiteSpace(withoutSlashes) && !withoutSlashes.Contains('/'))
        {
            host = withoutSlashes;
            return true;
        }

        return false;
    }

    private static NasPathResult ConnectToNasShare(string shareRoot, string username, string password)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "net",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.StartInfo.ArgumentList.Add("use");
            process.StartInfo.ArgumentList.Add(shareRoot);
            process.StartInfo.ArgumentList.Add($"/user:{username}");
            process.StartInfo.ArgumentList.Add(password);
            process.StartInfo.ArgumentList.Add("/persistent:no");

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                return new NasPathResult(true, shareRoot, "SMB 连接成功。");
            }

            var message = string.IsNullOrWhiteSpace(error) ? output : error;
            return new NasPathResult(false, shareRoot, $"SMB 连接失败：{message.Trim()}");
        }
        catch (Exception ex)
        {
            return new NasPathResult(false, shareRoot, $"SMB 连接失败：{ex.Message}");
        }
    }
}

