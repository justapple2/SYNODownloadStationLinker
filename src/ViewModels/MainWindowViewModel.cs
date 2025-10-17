using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SYNODownloadStationLinker.Models;
using Timer = System.Timers.Timer;

namespace SYNODownloadStationLinker.ViewModels;

[JsonSerializable(typeof(MainWindowViewModel))]
public partial class MainWindowViewModel : ViewModelBase
{
    [JsonIgnore] private SYNODSApiHandler apiHandler;
    [JsonIgnore] private AESKey aesKey;
    private IClipboard? clipboard;
    private System.Timers.Timer listenThread;
    private ManualResetEvent waitHandle;
    private BlockingCollection<string> blckQueues;
   
    private string oldClipboardText = string.Empty;
    private string configFile => $"{AppDomain.CurrentDomain.BaseDirectory}\\SYNODownloadStationLinker.json";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartListeningCommand))]
    private bool isListening;
    [ObservableProperty] private string uri = string.Empty;
    [ObservableProperty] private string userName = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string denision = string.Empty;
    [ObservableProperty] private string downloadPath = string.Empty;

    [JsonIgnore][ObservableProperty] private string response = string.Empty;
    
    
    [JsonIgnore][ObservableProperty][NotifyCanExecuteChangedFor(nameof(StartListeningCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreadAndDownloadCommand))]
    private bool isLogIn  = false;

    private bool CanBeListening() => !IsListening && IsLogIn;
    
    public void OnLoaded()
    {
        try
        {
           var data= Load();
           aesKey = AESKey.Load();
           aesKey.GetArrayBytes(out byte[] key, out byte[] iv);
           
           this.Uri = data.Uri;

           if (!string.IsNullOrWhiteSpace(data.UserName))
               this.UserName = AesEncryption.Decrypt4String(data.UserName, key, iv);
           if (!string.IsNullOrWhiteSpace(data.Password))
               this.Password = AesEncryption.Decrypt4String(data.Password, key, iv);
         
           this.Denision = data.Denision;
           this.DownloadPath = data.DownloadPath;
        }
        catch 
        {
            
        }
        if (App.Current is not App cur) return;
       
        clipboard = TopLevel.GetTopLevel(cur.CurrentWindow)?.Clipboard;
    }

    public async Task OnClosed()
    {
        try
        {
            Save();
            await apiHandler?.LogeOut();
            apiHandler?.Dispose();
        }
        catch
        {
        }
    }

    [RelayCommand(CanExecute = nameof(CanBeListening))]
    private async void StartListening()
    {
        if (listenThread is null)
        {
            listenThread = new Timer()
            {
                Interval = 100,
                AutoReset = true
            };
            listenThread.Elapsed += lisenTimer;
        }

        IsListening = true;
        waitHandle = new ManualResetEvent(false);
        blckQueues = new BlockingCollection<string>();
        listenThread.Start();
    }

    private async void lisenTimer(object? sender, ElapsedEventArgs e)
    {
        if(!IsListening) return;
        var clibText = await clipboard?.GetTextAsync();
        if (clibText is null) return;
        if (oldClipboardText != clibText && IsDownloadLink(clibText))
        {
            oldClipboardText = clibText;
            var createTime= DateTime.Now.ToString("g");
            try
            {
                NasPathHelper.GetLocalNASPath(this.Uri, this.Denision);
            }
            catch
            {
            }
            
            var resp = await apiHandler.CreateTaskAsync(oldClipboardText, this.Denision);
            var sb = new StringBuilder();
            sb.AppendLine(createTime);
            sb.AppendLine(oldClipboardText);
            sb.AppendLine(JsonHelper.Format(resp));
            Response = sb.ToString();
        }
    }
    
    string ValidateLink(string link)
    {
        Regex Ed2kRegex = new Regex(
            @"^(ed2k://\|file\|[a-zA-Z0-9_.-]+\|\d+\|[0-9a-fA-F]{32}\|/)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        Regex MagnetRegex = new Regex(
            @"^(magnet:\?xt=urn:btih:)([0-9a-fA-F]{40}|[0-9a-fA-F]{32}|[2-7A-Za-z]{32})(&[a-zA-Z0-9_.-]+=[a-zA-Z0-9_.-]+)*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        if (string.IsNullOrWhiteSpace(link))
            return null;

        if (MagnetRegex.IsMatch(link))
            return "magnet";

        if (Ed2kRegex.IsMatch(link))
            return "ed2k";

        return null;
    }
    
    
    private bool IsDownloadLink(string text)
    {
        if (text.StartsWith("magnet:?xt=urn:btih:") || text.StartsWith("ed2k:"))
        {
            return !string.IsNullOrWhiteSpace(ValidateLink(text));
        }
        // 简单检查是否为 URL
        if (System.Uri.TryCreate(text, UriKind.Absolute, out Uri uri))
        {
            // 检查是否为常见下载文件扩展名或特定模式
            string[] downloadExtensions = [".exe", ".zip", ".rar", ".msi", ".dmg", ".pkg", ".deb", ".rpm", ".iso", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".jpg", ".png", ".gif", ".mp4", ".mp3", ".avi", ".mov", ".txt", ".7z"
            ];
            string path = uri.AbsolutePath.ToLower();
            return downloadExtensions.Any(ext => path.EndsWith(ext));
        }
        
        return false;
    }

    [RelayCommand]
    private async void Init()
    {
        apiHandler = new SYNODSApiHandler(Uri);
        Response = await apiHandler.LogeIn(UserName, Password);
        Response = JsonHelper.Format(Response);
        if (Response.Contains("success"))
        {
          IsLogIn = true;
        }

    }
    [RelayCommand]
    private async void Test()
    {
        if (apiHandler == null)
        {
            Response = "Has not set, Error";
            return;
        }

        var res = await apiHandler.GetServerInfoAsync();
        Response = res;
        Response = JsonHelper.Format(res);
    }

    [RelayCommand(CanExecute = nameof(IsLogIn))]
    private async void CreadAndDownload()
    {
        if (apiHandler == null)
        {
            Response = "Has not set, Error";
            return;
        }

        try
        {
            NasPathHelper.GetLocalNASPath(this.Uri, this.Denision);
        }
        catch
        {
        }
       
        var sb = new StringBuilder();
        sb.AppendLine(DateTime.Now.ToString("g"));
        var resp = await apiHandler.CreateTaskAsync(this.DownloadPath, Denision, this.UserName, Password);
        sb.AppendLine(this.Denision);
        sb.AppendLine(JsonHelper.Format(resp));
        Response = sb.ToString();
    }
    
    [RelayCommand]
    private void Save()
    {
        using var sw = new StreamWriter(configFile, false, System.Text.Encoding.UTF8);
        aesKey.GetArrayBytes(out byte[] key, out byte[] iv);
        var saved = this.MemberwiseClone() as MainWindowViewModel;
        if(saved is null) return;
        saved.UserName = AesEncryption.Encrypt2String(saved.UserName, key, iv);
        saved.Password = AesEncryption.Encrypt2String(saved.Password, key, iv);
        var json = JsonHelper.Serialize(saved);
        sw.Write(json);
    }

    public MainWindowViewModel Load()
    {
        if (!File.Exists(configFile)) return new MainWindowViewModel();
        try
        {
            var json = File.ReadAllText(configFile);
            var model = JsonHelper.Deserialize<MainWindowViewModel>(json);
            //Init();
            return model;
        }
        catch (Exception e)
        {
            return new MainWindowViewModel();
        }
    }

    
}