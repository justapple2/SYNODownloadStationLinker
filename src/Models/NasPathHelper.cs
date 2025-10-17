using System;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace SYNODownloadStationLinker.Models;

public class NasPathHelper
{
    public static string GetLocalNASPath(string nasUrl, string downloadPath, string username="", string password="")
    {
        var msg = string.Empty;
        // 1. 验证是否为 IPv4 地址
        if (!IsIPv4Url(nasUrl, out string ip))
        {
            msg = "不是有效的 IPv4 地址";
            return null;
        }

        // 2. 构造 UNC 路径
        string uncPath = $@"\\{ip}{downloadPath.Replace('/', '\\')}";

        // 3. 创建文件夹（如果不存在）
        try
        {
            if (!Directory.Exists(uncPath))
            {
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    msg = "路径不存在，尝试连接 NAS...";
                    ConnectToNAS(ip, username, password);
                }
                Directory.CreateDirectory(uncPath);
                msg="文件夹已创建：" + uncPath;
            }
        }
        catch (Exception ex)
        {
            msg = "创建文件夹失败：" + ex.Message;
            return null;
        }

        return uncPath;
    }

    private static bool IsIPv4Url(string url, out string ip)
    {
        ip = null;
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
        {
            string host = uri.Host;
            if (IPAddress.TryParse(host, out IPAddress address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                ip = host;
                return true;
            }
        }
        return false;
    }

    private static void ConnectToNAS(string ip, string username, string password)
    {
        string uncRoot = $@"\\{ip}";
        string arguments = $@"use {uncRoot} /user:{username} {password}";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "net",
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}