using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SYNODownloadStationLinker.Models;

public sealed class SynologyResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("error")]
    public SynologyError? Error { get; set; }
}

public sealed class SynologyError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("errors")]
    public IReadOnlyList<SynologyFieldError>? Errors { get; set; }

    public override string ToString() => Code == 0 ? "Unknown error" : $"Error code {Code}";
}

public sealed class SynologyFieldError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }
}

public sealed class SynologyApiInfo
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("minVersion")]
    public int MinVersion { get; set; }

    [JsonPropertyName("maxVersion")]
    public int MaxVersion { get; set; }
}

public sealed class DownloadStationInfo
{
    [JsonPropertyName("is_manager")]
    public bool IsManager { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("version_string")]
    public string VersionString { get; set; } = string.Empty;
}

public sealed class DownloadStationConfig
{
    [JsonPropertyName("default_destination")]
    public string DefaultDestination { get; set; } = string.Empty;

    [JsonPropertyName("emule_default_destination")]
    public string EmuleDefaultDestination { get; set; } = string.Empty;

    [JsonPropertyName("bt_max_download")]
    public int BtMaxDownload { get; set; }

    [JsonPropertyName("http_max_download")]
    public int HttpMaxDownload { get; set; }

    [JsonPropertyName("nzb_max_download")]
    public int NzbMaxDownload { get; set; }
}

public sealed class DownloadTaskList
{
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("tasks")]
    public List<DownloadTask> Tasks { get; set; } = [];
}

public sealed class DownloadTask
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("status_extra")]
    public DownloadTaskStatusExtra? StatusExtra { get; set; }

    [JsonPropertyName("additional")]
    public DownloadTaskAdditional? Additional { get; set; }
}

public sealed class DownloadTaskStatusExtra
{
    [JsonPropertyName("error_detail")]
    public string? ErrorDetail { get; set; }
}

public sealed class DownloadTaskAdditional
{
    [JsonPropertyName("detail")]
    public DownloadTaskDetail? Detail { get; set; }

    [JsonPropertyName("transfer")]
    public DownloadTaskTransfer? Transfer { get; set; }

    [JsonPropertyName("file")]
    public IReadOnlyList<DownloadTaskFile>? Files { get; set; }
}

public sealed class DownloadTaskDetail
{
    [JsonPropertyName("destination")]
    public string Destination { get; set; } = string.Empty;

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("create_time")]
    public long CreateTime { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;
}

public sealed class DownloadTaskTransfer
{
    [JsonPropertyName("size_downloaded")]
    public long SizeDownloaded { get; set; }

    [JsonPropertyName("size_uploaded")]
    public long SizeUploaded { get; set; }

    [JsonPropertyName("speed_download")]
    public long SpeedDownload { get; set; }

    [JsonPropertyName("speed_upload")]
    public long SpeedUpload { get; set; }
}

public sealed class DownloadTaskFile
{
    [JsonPropertyName("filename")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("size_downloaded")]
    public long SizeDownloaded { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;
}

public sealed record NasPathResult(bool Success, string? UncPath, string Message);

