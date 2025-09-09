using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace SYNODownloadStationLinker.Models;

public class SYNODSApiHandler
{
    readonly string _uri = string.Empty;

    private HttpClient _client;

    private readonly HttpClientHandler _handler = new()
        { ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) => true };

    public SYNODSApiHandler(string uri)
    {
        _uri = uri;
    }

    public async Task<string> CreateTaskAsync(string url, string destination, string user = "", string password = "")
    {
        var uri = createDownloadCreate(url, destination, user, password);
        return await RequireAndResponseAsync(uri);
    }

    public async Task<string> GetServerInfoAsync() => await RequireAndResponseAsync(createGetInfoConnect());

    public async Task<string> GetList(params string[] paras) =>
        await RequireAndResponseAsync(createDownloadListCommand(paras));
    
    
    public async Task<string> LogeIn(string username, string password) =>
        await RequireAndResponseAsync(createLoginConnect(username, password));

    public async Task<string> LogeOut() => await RequireAndResponseAsync(createLogoutConnect());

    public async Task<string> GetInfo() => await RequireAndResponseAsync(createGetInfo());
    public async Task<string> GetConfig() => await RequireAndResponseAsync(createGetConfig());

    private async Task<string> RequireAndResponseAsync(string url)
    {
        if (_client == null) _client = new HttpClient(_handler);
        var dst = string.Empty;
        try
        {
            HttpResponseMessage response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            dst = await response.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            dst = $"{e}";
        }

        return dst;
    }


    public void Dispose()
    {
        _client?.Dispose();
    }

    private string createGetInfoConnect()
    {
        var uri = _uri.TrimEnd('/');
        return
            $"{uri}/webapi/query.cgi?api=SYNO.API.Info&version=1&method=query&query=SYNO.API.Auth,SYNO.DownloadStation.Task";
    }

    private string createLoginConnect(string user, string password)
    {
        var uri = _uri.TrimEnd('/');
        return
            $"{uri}/webapi/auth.cgi?api=SYNO.API.Auth&version=7&method=login&account={user}&passwd={password}&session=DownloadStation&format=cookie";
    }

    private string createLogoutConnect()
    {
        var uri = _uri.TrimEnd('/');
        return $"{uri}/webapi/auth.cgi?api=SYNO.API.Auth&version=1&method=logout&session=DownloadStation";
    }

    private string createDownloadHead(DownloadStationMothed method, CgiTypes apiType)
    {
        var uri = _uri.TrimEnd('/');
        return
            $"{uri}/webapi/DownloadStation/{apiType.ToString().ToLower()}.cgi?api=SYNO.DownloadStation.{apiType}&version=1&method={method}";
    }

    private string createGetInfo() => createDownloadHead(DownloadStationMothed.getinfo, CgiTypes.Info);

    private string createGetConfig() => createDownloadHead(DownloadStationMothed.getconfig, CgiTypes.Info);

    private string createDownloadListCommand(params string[] accect)
    {
        var paras = string.Join(",", accect).TrimEnd(',');
        return $"{createDownloadHead(DownloadStationMothed.list, CgiTypes.Task)}&additional={paras}";
    }

    private string createDownloadCreate(string url, string destination="", string user = "", string password = "")
    {
        var end2 = $"&destination={destination}";
        if(string.IsNullOrEmpty(destination)) end2=string.Empty;
        var end = $"&username={user}&password={password}";
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password)) end = string.Empty;
        return
            $"{createDownloadHead(DownloadStationMothed.create, CgiTypes.Task)}&uri={url}{end}{end2}";
    }
}