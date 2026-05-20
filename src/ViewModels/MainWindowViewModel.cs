using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SYNODownloadStationLinker.Models;

namespace SYNODownloadStationLinker.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static readonly Regex Ed2kRegex = new(
        @"^ed2k://\|file\|.+\|\d+\|[0-9a-fA-F]{32}\|/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MagnetRegex = new(
        @"^magnet:\?xt=urn:btih:[a-zA-Z0-9]{32,40}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [JsonIgnore] private SYNODSApiHandler? apiHandler;
    [JsonIgnore] private AESKey? aesKey;
    [JsonIgnore] private IClipboard? clipboard;
    [JsonIgnore] private CancellationTokenSource? listenCts;
    [JsonIgnore] private readonly SemaphoreSlim createLock = new(1, 1);
    [JsonIgnore] private string oldClipboardText = string.Empty;

    private string ConfigFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SYNODownloadStationLinker.json");
    private string LogFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "SYNODownloadStationLinker.log");

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartListeningCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopListeningCommand))]
    private bool isListening;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartListeningCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateAndDownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshTasksCommand))]
    private bool isLogIn;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartListeningCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateAndDownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshTasksCommand))]
    private bool isBusy;

    [JsonIgnore] [ObservableProperty] private bool isPaneOpen = true;
    [JsonIgnore] [ObservableProperty] private int selectedWorkspaceIndex;

    [ObservableProperty] private string uri = string.Empty;
    [ObservableProperty] private string userName = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [property: JsonPropertyName("Denision")]
    [ObservableProperty] private string destination = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateAndDownloadCommand))]
    private string downloadPath = string.Empty;

    [JsonIgnore] [ObservableProperty] private string response = string.Empty;
    [JsonIgnore] [ObservableProperty] private string statusText = "未登录";
    [JsonIgnore] [ObservableProperty] private string latestTaskTitle = "暂无任务";
    [JsonIgnore] [ObservableProperty] private string latestTaskStatus = "-";
    [JsonIgnore] [ObservableProperty] private double latestProgress;

    [JsonIgnore] public ObservableCollection<DownloadLogEntry> Logs { get; } = [];
    [JsonIgnore] public ObservableCollection<DownloadTaskProgress> Tasks { get; } = [];

    public event Action<NotificationMessage>? NotificationRequested;

    private bool CanStartListening() => !IsListening && IsLogIn && !IsBusy;
    private bool CanStopListening() => IsListening;
    private bool CanCreateAndDownload() => IsLogIn && !IsBusy && !string.IsNullOrWhiteSpace(DownloadPath);
    private bool CanRefreshTasks() => IsLogIn && !IsBusy;

    public void OnLoaded()
    {
        aesKey = AESKey.Load();

        try
        {
            var data = Load();
            aesKey.GetArrayBytes(out var key, out var iv);

            Uri = data.Uri;
            UserName = string.IsNullOrWhiteSpace(data.UserName)
                ? string.Empty
                : AesEncryption.Decrypt4String(data.UserName, key, iv);
            Password = string.IsNullOrWhiteSpace(data.Password)
                ? string.Empty
                : AesEncryption.Decrypt4String(data.Password, key, iv);
            Destination = data.Destination;
            DownloadPath = data.DownloadPath;
            StatusText = "配置已加载";
        }
        catch (Exception ex)
        {
            AddLog("WARN", "加载配置失败", ex.Message);
        }

        if (Application.Current is App cur)
        {
            clipboard = TopLevel.GetTopLevel(cur.CurrentWindow)?.Clipboard;
        }
    }

    public async Task OnClosed()
    {
        try
        {
            StopListening();
            Save();
            if (apiHandler is not null)
            {
                await apiHandler.LogoutAsync();
                apiHandler.Dispose();
            }
        }
        catch (Exception ex)
        {
            AddLog("WARN", "关闭时发生异常", ex.Message);
        }
    }

    [RelayCommand]
    private async Task Init()
    {
        IsBusy = true;
        try
        {
            apiHandler?.Dispose();
            apiHandler = new SYNODSApiHandler(Uri);

            await apiHandler.QueryApiInfoAsync();
            var login = await apiHandler.LoginAsync(UserName, Password);
            Response = PrettyJson(apiHandler.LastRawResponse);
            IsLogIn = login.Success;
            StatusText = login.Success ? "已登录 Download Station" : $"登录失败：{DescribeError(login.Error)}";

            AddLog(login.Success ? "INFO" : "ERROR", "登录", StatusText);
            Notify(login.Success ? "登录成功" : "登录失败", StatusText, login.Success ? NotificationSeverity.Success : NotificationSeverity.Error);

            if (login.Success)
            {
                await RefreshTasksCoreAsync(updateBusy: false);
            }
        }
        catch (Exception ex)
        {
            IsLogIn = false;
            StatusText = $"登录异常：{ex.Message}";
            Response = ex.ToString();
            AddLog("ERROR", "登录异常", ex.Message);
            Notify("登录异常", ex.Message, NotificationSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Test()
    {
        if (apiHandler is null)
        {
            Response = "尚未登录。";
            return;
        }

        IsBusy = true;
        try
        {
            var info = await apiHandler.GetInfoAsync();
            var config = await apiHandler.GetConfigAsync();
            var sb = new StringBuilder();
            sb.AppendLine("Download Station");
            sb.AppendLine(info.Success
                ? $"{info.Data?.VersionString} (API {info.Data?.Version})"
                : DescribeError(info.Error));
            sb.AppendLine();
            sb.AppendLine("Config");
            sb.AppendLine(config.Success
                ? $"Default destination: {config.Data?.DefaultDestination}"
                : DescribeError(config.Error));
            Response = sb.ToString();
            StatusText = info.Success ? "连接测试通过" : "连接测试失败";
            AddLog(info.Success ? "INFO" : "ERROR", "连接测试", StatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartListening))]
    private void StartListening()
    {
        if (clipboard is null)
        {
            Notify("无法监听剪贴板", "当前窗口未提供剪贴板访问。", NotificationSeverity.Error);
            return;
        }

        listenCts?.Cancel();
        listenCts = new CancellationTokenSource();
        IsListening = true;
        StatusText = "正在监听剪贴板";
        _ = ListenClipboardAsync(listenCts.Token);
        AddLog("INFO", "剪贴板监听", "已启动");
    }

    [RelayCommand(CanExecute = nameof(CanStopListening))]
    private void StopListening()
    {
        listenCts?.Cancel();
        listenCts?.Dispose();
        listenCts = null;
        IsListening = false;
        if (IsLogIn)
        {
            StatusText = "已登录 Download Station";
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateAndDownload))]
    private async Task CreateAndDownload()
    {
        await CreateDownloadFromLinkAsync(DownloadPath, "手动创建");
    }

    [RelayCommand(CanExecute = nameof(CanRefreshTasks))]
    private async Task RefreshTasks()
    {
        await RefreshTasksCoreAsync();
    }

    [RelayCommand]
    private void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    [RelayCommand]
    private void NavigateToWorkspace(string index)
    {
        if (int.TryParse(index, out var tabIndex))
        {
            SelectedWorkspaceIndex = tabIndex;
        }
    }

    private async Task RefreshTasksCoreAsync(
        string? preferredLink = null,
        bool updateBusy = true,
        bool preserveResponse = false)
    {
        if (apiHandler is null)
        {
            return;
        }

        if (updateBusy)
        {
            IsBusy = true;
        }

        try
        {
            var result = await apiHandler.ListTasksAsync(limit: 10);
            if (!preserveResponse)
            {
                Response = PrettyJson(apiHandler.LastRawResponse);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var taskItems = (result.Data?.Tasks ?? [])
                    .Select(DownloadTaskProgress.FromTask)
                    .OrderByDescending(task => task.CreateTime)
                    .Take(10)
                    .ToList();

                var matchedTask = string.IsNullOrWhiteSpace(preferredLink)
                    ? null
                    : taskItems.FirstOrDefault(task => IsSameLink(task.SourceUri, preferredLink));

                if (matchedTask is not null)
                {
                    taskItems.Remove(matchedTask);
                    taskItems.Insert(0, matchedTask);
                }

                Tasks.Clear();
                foreach (var task in taskItems)
                {
                    Tasks.Add(task);
                }

                var latest = matchedTask ?? Tasks.FirstOrDefault();
                LatestTaskTitle = latest?.Title ?? "暂无任务";
                LatestTaskStatus = latest?.Status ?? "-";
                LatestProgress = latest?.Progress ?? 0;
            });

            StatusText = result.Success ? "任务列表已刷新" : $"刷新失败：{DescribeError(result.Error)}";
        }
        catch (Exception ex)
        {
            StatusText = $"刷新异常：{ex.Message}";
            AddLog("ERROR", "刷新任务异常", ex.Message);
        }
        finally
        {
            if (updateBusy)
            {
                IsBusy = false;
            }
        }
    }

    private async Task RefreshTasksUntilVisibleAsync(string link)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await RefreshTasksCoreAsync(link, updateBusy: false, preserveResponse: true);
            if (Tasks.Any(task => IsSameLink(task.SourceUri, link)))
            {
                StatusText = "已定位新建任务状态";
                return;
            }

            await Task.Delay(900);
        }

        StatusText = "任务已创建，等待 Download Station 返回最新状态";
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            aesKey ??= AESKey.Load();
            aesKey.GetArrayBytes(out var key, out var iv);

            var saved = (MainWindowViewModel)MemberwiseClone();
            saved.UserName = AesEncryption.Encrypt2String(saved.UserName, key, iv);
            saved.Password = AesEncryption.Encrypt2String(saved.Password, key, iv);
            var json = JsonHelper.Serialize(saved, indented: true);
            File.WriteAllText(ConfigFile, json, Encoding.UTF8);
            AddLog("INFO", "保存配置", "配置已保存");
        }
        catch (Exception ex)
        {
            AddLog("ERROR", "保存配置失败", ex.Message);
            Notify("保存配置失败", ex.Message, NotificationSeverity.Error);
        }
    }

    public MainWindowViewModel Load()
    {
        if (!File.Exists(ConfigFile))
        {
            return new MainWindowViewModel();
        }

        try
        {
            var json = File.ReadAllText(ConfigFile, Encoding.UTF8);
            return JsonHelper.Deserialize<MainWindowViewModel>(json) ?? new MainWindowViewModel();
        }
        catch
        {
            return new MainWindowViewModel();
        }
    }

    private async Task ListenClipboardAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var clipText = await clipboard!.GetTextAsync();
                if (!string.IsNullOrWhiteSpace(clipText)
                    && !string.Equals(oldClipboardText, clipText, StringComparison.Ordinal)
                    && IsDownloadLink(clipText))
                {
                    oldClipboardText = clipText;
                    await CreateDownloadFromLinkAsync(clipText, "剪贴板");
                }

                await Task.Delay(800, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AddLog("ERROR", "监听剪贴板异常", ex.Message);
                await Task.Delay(1500, cancellationToken);
            }
        }
    }

    private async Task CreateDownloadFromLinkAsync(string link, string source)
    {
        if (apiHandler is null)
        {
            Notify("尚未登录", "请先登录 NAS。", NotificationSeverity.Error);
            return;
        }

        if (!IsDownloadLink(link))
        {
            Notify("链接不可用", "剪贴板或输入框内容不是受支持的下载地址。", NotificationSeverity.Warning);
            return;
        }

        await createLock.WaitAsync();
        IsBusy = true;
        try
        {
            StatusText = "正在准备下载目录";
            var nasPath = await Task.Run(() => NasPathHelper.EnsureDirectory(Uri, Destination, UserName, Password));
            AddLog(nasPath.Success ? "INFO" : "ERROR", "下载目录", nasPath.Message);
            if (!nasPath.Success)
            {
                Response = $"{DateTime.Now:g}{Environment.NewLine}{link}{Environment.NewLine}{nasPath.Message}";
                Notify("目录创建失败", nasPath.Message, NotificationSeverity.Error);
                return;
            }

            StatusText = "正在创建下载任务";
            var result = await apiHandler.CreateTaskAsync(link, Destination);
            Response = BuildCreateTaskResponse(source, link, nasPath, result);

            if (result.Success)
            {
                AddLog("INFO", "创建下载任务成功", link);
                Notify("任务已创建", Shorten(link), NotificationSeverity.Success);
                SelectedWorkspaceIndex = 0;
                StatusText = "任务已创建，正在读取最新状态";
                await RefreshTasksUntilVisibleAsync(link);
            }
            else
            {
                var message = DescribeError(result.Error);
                AddLog("ERROR", "创建下载任务失败", $"{message} | {link}");
                Notify("任务创建失败", message, NotificationSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            Response = ex.ToString();
            AddLog("ERROR", "创建下载任务异常", ex.Message);
            Notify("任务创建异常", ex.Message, NotificationSeverity.Error);
        }
        finally
        {
            IsBusy = false;
            createLock.Release();
        }
    }

    private string BuildCreateTaskResponse(
        string source,
        string link,
        NasPathResult nasPath,
        SynologyResponse<object> result)
    {
        var sb = new StringBuilder();
        sb.AppendLine(DateTime.Now.ToString("g"));
        sb.AppendLine($"来源：{source}");
        sb.AppendLine($"目录：{nasPath.Message}");
        sb.AppendLine($"链接：{link}");
        sb.AppendLine(result.Success ? "结果：创建成功" : $"结果：创建失败 - {DescribeError(result.Error)}");
        sb.AppendLine();
        sb.AppendLine(PrettyJson(apiHandler?.LastRawResponse ?? string.Empty));
        return sb.ToString();
    }

    private static bool IsDownloadLink(string text)
    {
        var value = text.Trim();
        if (MagnetRegex.IsMatch(value) || Ed2kRegex.IsMatch(value))
        {
            return true;
        }

        if (!System.Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not ("http" or "https" or "ftp"))
        {
            return false;
        }

        string[] downloadExtensions =
        [
            ".exe", ".zip", ".rar", ".msi", ".dmg", ".pkg", ".deb", ".rpm", ".iso", ".pdf",
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".jpg", ".png", ".gif",
            ".mp4", ".mp3", ".avi", ".mov", ".mkv", ".txt", ".7z", ".tar", ".gz"
        ];
        var path = uri.AbsolutePath.ToLowerInvariant();
        return downloadExtensions.Any(path.EndsWith);
    }

    private static bool IsSameLink(string? first, string? second)
    {
        return !string.IsNullOrWhiteSpace(first)
               && !string.IsNullOrWhiteSpace(second)
               && string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void Notify(string title, string message, NotificationSeverity severity)
    {
        NotificationRequested?.Invoke(new NotificationMessage(title, message, severity));
    }

    private void AddLog(string level, string title, string message)
    {
        var entry = new DownloadLogEntry(DateTime.Now, level, title, message);

        void AddToCollection()
        {
            Logs.Insert(0, entry);
            while (Logs.Count > 100)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            AddToCollection();
        }
        else
        {
            Dispatcher.UIThread.Post(AddToCollection);
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            File.AppendAllText(LogFile, entry.ToLogLine() + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // Logging must never break task creation.
        }
    }

    private static string PrettyJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            return JsonHelper.Format(json);
        }
        catch
        {
            return json;
        }
    }

    private static string DescribeError(SynologyError? error)
    {
        return error is null ? "未知错误" : $"DSM 错误码 {error.Code}";
    }

    private static string Shorten(string value)
    {
        value = value.Trim();
        return value.Length <= 96 ? value : $"{value[..96]}...";
    }
}

public sealed record NotificationMessage(string Title, string Message, NotificationSeverity Severity);

public enum NotificationSeverity
{
    Success,
    Warning,
    Error
}

public sealed record DownloadLogEntry(DateTime Time, string Level, string Title, string Message)
{
    public string TimeText => Time.ToString("HH:mm:ss");
    public string Summary => $"{Title} - {Message}";
    public string ToLogLine() => $"{Time:yyyy-MM-dd HH:mm:ss}\t{Level}\t{Title}\t{Message}";
}

public sealed class DownloadTaskProgress
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string SourceUri { get; init; } = string.Empty;
    public long CreateTime { get; init; }
    public double Progress { get; init; }
    public string ProgressText => $"{Progress:0.#}%";
    public string SizeText { get; init; } = string.Empty;
    public string SpeedText { get; init; } = string.Empty;

    public static DownloadTaskProgress FromTask(DownloadTask task)
    {
        var downloaded = task.Additional?.Transfer?.SizeDownloaded ?? 0;
        var size = task.Size;
        var progress = size <= 0 ? 0 : Math.Clamp(downloaded * 100d / size, 0, 100);

        return new DownloadTaskProgress
        {
            Id = task.Id,
            Title = string.IsNullOrWhiteSpace(task.Title) ? task.Id : task.Title,
            Status = task.Status,
            Destination = task.Additional?.Detail?.Destination ?? string.Empty,
            SourceUri = task.Additional?.Detail?.Uri ?? string.Empty,
            CreateTime = task.Additional?.Detail?.CreateTime ?? 0,
            Progress = progress,
            SizeText = $"{FormatBytes(downloaded)} / {FormatBytes(size)}",
            SpeedText = $"{FormatBytes(task.Additional?.Transfer?.SpeedDownload ?? 0)}/s"
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = Math.Max(0, bytes);
        var unit = 0;
        var value = (double)size;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
