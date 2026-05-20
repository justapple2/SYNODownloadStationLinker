using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SYNODownloadStationLinker.Models;

public sealed class SYNODSApiHandler : IDisposable
{
    private const string DownloadStationSession = "DownloadStation";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _baseUri;
    private readonly CookieContainer _cookies = new();
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _client;
    private readonly Dictionary<string, SynologyApiInfo> _apiInfo = new(StringComparer.OrdinalIgnoreCase);

    public string LastRawResponse { get; private set; } = string.Empty;

    public SYNODSApiHandler(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new ArgumentException("NAS address can not be empty.", nameof(uri));
        }

        _baseUri = uri.TrimEnd('/');
        _handler = new HttpClientHandler
        {
            CookieContainer = _cookies,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        _client = new HttpClient(_handler);
    }

    public async Task<SynologyResponse<Dictionary<string, SynologyApiInfo>>> QueryApiInfoAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<Dictionary<string, SynologyApiInfo>>(
            "/webapi/query.cgi",
            new Dictionary<string, string?>
            {
                ["api"] = "SYNO.API.Info",
                ["version"] = "1",
                ["method"] = "query",
                ["query"] = string.Join(',', new[]
                {
                    "SYNO.API.Auth",
                    "SYNO.DownloadStation.Info",
                    "SYNO.DownloadStation.Task",
                    "SYNO.DownloadStation.Statistic",
                    "SYNO.DownloadStation.Schedule",
                    "SYNO.FileStation.CreateFolder"
                })
            },
            cancellationToken);

        if (result.Success && result.Data is not null)
        {
            _apiInfo.Clear();
            foreach (var item in result.Data)
            {
                _apiInfo[item.Key] = item.Value;
            }
        }

        return result;
    }

    public async Task<SynologyResponse<object>> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var version = GetVersion("SYNO.API.Auth", fallback: 7);
        return await GetAsync<object>(
            GetPath("SYNO.API.Auth", "auth.cgi"),
            new Dictionary<string, string?>
            {
                ["api"] = "SYNO.API.Auth",
                ["version"] = version.ToString(),
                ["method"] = "login",
                ["account"] = username,
                ["passwd"] = password,
                ["session"] = DownloadStationSession,
                ["format"] = "cookie"
            },
            cancellationToken);
    }

    public Task<SynologyResponse<object>> LogoutAsync(CancellationToken cancellationToken = default)
    {
        return GetAsync<object>(
            GetPath("SYNO.API.Auth", "auth.cgi"),
            new Dictionary<string, string?>
            {
                ["api"] = "SYNO.API.Auth",
                ["version"] = GetVersion("SYNO.API.Auth", fallback: 1).ToString(),
                ["method"] = "logout",
                ["session"] = DownloadStationSession
            },
            cancellationToken);
    }

    public Task<SynologyResponse<DownloadStationInfo>> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        return GetDownloadStationAsync<DownloadStationInfo>(
            "SYNO.DownloadStation.Info",
            "Info",
            "getinfo",
            cancellationToken: cancellationToken);
    }

    public Task<SynologyResponse<DownloadStationConfig>> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        return GetDownloadStationAsync<DownloadStationConfig>(
            "SYNO.DownloadStation.Info",
            "Info",
            "getconfig",
            cancellationToken: cancellationToken);
    }

    public Task<SynologyResponse<DownloadTaskList>> ListTasksAsync(
        int offset = 0,
        int limit = 20,
        string additional = "detail,transfer",
        CancellationToken cancellationToken = default)
    {
        return GetDownloadStationAsync<DownloadTaskList>(
            "SYNO.DownloadStation.Task",
            "Task",
            "list",
            new Dictionary<string, string?>
            {
                ["offset"] = offset.ToString(),
                ["limit"] = limit.ToString(),
                ["additional"] = additional
            },
            cancellationToken);
    }

    public Task<SynologyResponse<object>> CreateTaskAsync(
        string url,
        string destination,
        string downloadUser = "",
        string downloadPassword = "",
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["uri"] = url,
            ["destination"] = NormalizeDestination(destination)
        };

        if (!string.IsNullOrWhiteSpace(downloadUser))
        {
            parameters["username"] = downloadUser;
        }

        if (!string.IsNullOrWhiteSpace(downloadPassword))
        {
            parameters["password"] = downloadPassword;
        }

        return PostDownloadStationAsync<object>(
            "SYNO.DownloadStation.Task",
            "Task",
            "create",
            parameters,
            cancellationToken);
    }

    public Task<SynologyResponse<object>> PauseTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        return PostDownloadStationAsync<object>(
            "SYNO.DownloadStation.Task",
            "Task",
            "pause",
            new Dictionary<string, string?> { ["id"] = id },
            cancellationToken);
    }

    public Task<SynologyResponse<object>> ResumeTaskAsync(string id, CancellationToken cancellationToken = default)
    {
        return PostDownloadStationAsync<object>(
            "SYNO.DownloadStation.Task",
            "Task",
            "resume",
            new Dictionary<string, string?> { ["id"] = id },
            cancellationToken);
    }

    public Task<SynologyResponse<object>> DeleteTaskAsync(
        string id,
        bool forceComplete = false,
        CancellationToken cancellationToken = default)
    {
        return PostDownloadStationAsync<object>(
            "SYNO.DownloadStation.Task",
            "Task",
            "delete",
            new Dictionary<string, string?>
            {
                ["id"] = id,
                ["force_complete"] = forceComplete ? "true" : "false"
            },
            cancellationToken);
    }

    private async Task<SynologyResponse<T>> GetDownloadStationAsync<T>(
        string api,
        string fallbackCgi,
        string method,
        Dictionary<string, string?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var allParameters = CreateApiParameters(api, method, parameters);
        return await GetAsync<T>(GetPath(api, $"DownloadStation/{fallbackCgi.ToLowerInvariant()}.cgi"), allParameters,
            cancellationToken);
    }

    private async Task<SynologyResponse<T>> PostDownloadStationAsync<T>(
        string api,
        string fallbackCgi,
        string method,
        Dictionary<string, string?> parameters,
        CancellationToken cancellationToken = default)
    {
        var allParameters = CreateApiParameters(api, method, parameters);
        return await PostAsync<T>(GetPath(api, $"DownloadStation/{fallbackCgi.ToLowerInvariant()}.cgi"), allParameters,
            cancellationToken);
    }

    private Dictionary<string, string?> CreateApiParameters(
        string api,
        string method,
        Dictionary<string, string?>? parameters)
    {
        var result = new Dictionary<string, string?>
        {
            ["api"] = api,
            ["version"] = GetVersion(api, fallback: 1).ToString(),
            ["method"] = method
        };

        if (parameters is null)
        {
            return result;
        }

        foreach (var parameter in parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.Value))
            {
                result[parameter.Key] = parameter.Value;
            }
        }

        return result;
    }

    private async Task<SynologyResponse<T>> GetAsync<T>(
        string path,
        Dictionary<string, string?> parameters,
        CancellationToken cancellationToken)
    {
        var response = await _client.GetAsync(BuildUri(path, parameters), cancellationToken);
        return await ReadResponseAsync<T>(response, cancellationToken);
    }

    private async Task<SynologyResponse<T>> PostAsync<T>(
        string path,
        Dictionary<string, string?> parameters,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(CreateFormValues(parameters));
        var response = await _client.PostAsync(BuildUri(path, null), content, cancellationToken);
        return await ReadResponseAsync<T>(response, cancellationToken);
    }

    private async Task<SynologyResponse<T>> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        LastRawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new SynologyResponse<T>
            {
                Success = false,
                Error = new SynologyError { Code = (int)response.StatusCode }
            };
        }

        try
        {
            return JsonSerializer.Deserialize<SynologyResponse<T>>(LastRawResponse, JsonOptions)
                   ?? new SynologyResponse<T>
                   {
                       Success = false,
                       Error = new SynologyError { Code = -1 }
                   };
        }
        catch (JsonException)
        {
            return new SynologyResponse<T>
            {
                Success = false,
                Error = new SynologyError { Code = -2 }
            };
        }
    }

    private string BuildUri(string path, Dictionary<string, string?>? parameters)
    {
        var normalizedPath = path.StartsWith('/') ? path : $"/webapi/{path}";
        var builder = new StringBuilder($"{_baseUri}{normalizedPath}");

        if (parameters is null || parameters.Count == 0)
        {
            return builder.ToString();
        }

        builder.Append('?');
        var isFirst = true;
        foreach (var parameter in parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Value))
            {
                continue;
            }

            if (!isFirst)
            {
                builder.Append('&');
            }

            builder
                .Append(Uri.EscapeDataString(parameter.Key))
                .Append('=')
                .Append(Uri.EscapeDataString(parameter.Value));
            isFirst = false;
        }

        return builder.ToString();
    }

    private static IEnumerable<KeyValuePair<string, string>> CreateFormValues(Dictionary<string, string?> values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value.Value))
            {
                yield return new KeyValuePair<string, string>(value.Key, value.Value);
            }
        }
    }

    private string GetPath(string api, string fallback)
    {
        if (_apiInfo.TryGetValue(api, out var info) && !string.IsNullOrWhiteSpace(info.Path))
        {
            return info.Path.TrimStart('/');
        }

        return fallback;
    }

    private int GetVersion(string api, int fallback)
    {
        if (_apiInfo.TryGetValue(api, out var info) && info.MaxVersion > 0)
        {
            return info.MaxVersion;
        }

        return fallback;
    }

    private static string NormalizeDestination(string destination)
    {
        return destination.Replace('\\', '/').Trim('/');
    }

    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
    }
}

