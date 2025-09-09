using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;

namespace SYNODownloadStationLinker.Models;

public class SYNOApiInfo
{
    public CgiTypes CgiType { get; set; }
    public string ApiName { get; set; }
    public string ApiVersion { get; set; }
    public bool IsDownloadStationApi { get; set; } = false;
    public DownloadStationMothed Mothed { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

    public string CreateApiInfomation()
    {
        var isStation = IsDownloadStationApi ? $"/DownloadStation" : string.Empty;
        var get = $"/webapi{isStation}/{CgiType}.cgi?";
        var para = Parameters.Keys.Count == 0
            ? string.Empty
            : $"&{string.Join('&', Parameters.Select(p => $"{p.Key}={p.Value}")).TrimEnd('&')}";
        return $"{get}api={ApiName}&version={ApiVersion}{para}";
    }
}

public class Responses<T> :FormartToString<Responses<T>> where T : class
{
    public T Data { get; set; }

    public bool Success { get; set; }
}

//Info Response
public class ResponseInfo : FormartToString<ResponseInfo>
{
    public string Path { get; set; } = string.Empty;
    public int MinVersion { get; set; } = 0;
    public int MaxVersion { get; set; } = 0;

    public override string ToString()
    {
        return $"Path:{Path}{Environment.NewLine}MinVersion:{MinVersion}{Environment.NewLine}MaxVersion:{MaxVersion}";
    }
}

public class ResponseInfos:FormartToString<ResponseInfos>
{
    public Dictionary<string, ResponseInfo> Infos { get; set; } = [];

   
}

//DownloadStation Info Respnse

public class ResponseDownloadStationGetInfo:FormartToString<ResponseDownloadStationGetInfo>
{
    public bool Is_manager { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Version_string { get; set; } = string.Empty;

}

public class ResponseDownloadStationList:FormartToString<ResponseDownloadStationList>
{
    public int Total { get; set; }
    public int Offset { get; set; }
    public List<ResponseDownloadStationTask> Tasks { get; set; } = [];
}

public class ResponseDownloadStationTask:FormartToString<ResponseDownloadStationTask>
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Tiltle { get; set; } = string.Empty;
    public ulong Size { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Status_extra { get; set; } = string.Empty;
    public List<ResponseDownloadStationTaskAdditional> Additional = [];
}

public class ResponseDownloadStationTaskAdditional:FormartToString<ResponseDownloadStationTaskAdditional>
{
    public TaskDetail detail { get; set; } = new();
    public List<TaskFile> File { get; set; } = [];
}

public class TaskDetail:FormartToString<TaskDetail>
{
    public int Connected_leechers { get; set; }
    public int Connected_seeders { get; set; }
    public long Create_time { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int Total_peers { get; set; }
    public string Uri { get; set; } = string.Empty;
}

public class DownloadStationError:FormartToString<DownloadStationError>
{
    public string Error { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public class TaskFile:FormartToString<TaskFile>
{
    public string filename { get; set; }
    public string priority { get; set; }
    public ulong Size { get; set; }
    public ulong Size_downloaded { get; set; }

    public override string ToString()
    {
        return
            $"filename:{filename}{Environment.NewLine}priority:{priority}{Environment.NewLine}Size:{Size}{Environment.NewLine}Size_downloaded:{Size_downloaded}";
    }
}

public abstract class FormartToString<T> where T : class
{
    public override string ToString()
    {
        if (typeof(T) != this.GetType())
            throw new ArgumentException($"Type {typeof(T)} can not convert to {this.GetType()}");
        var props = typeof(T).GetProperties(System.Reflection.BindingFlags.Public |
                                            System.Reflection.BindingFlags.Instance);
        var sb = new StringBuilder();
        foreach (var prop in props)
        {
            // 跳过索引器
            if (prop.GetIndexParameters().Length > 0)
                continue;

            var val = prop.GetValue(this);
            var name = prop.Name;

            if (val is null)
            {
                sb.AppendLine($"{name}:null");
            }
            else if (prop.PropertyType.IsValueType || val is string)
            {
                sb.AppendLine($"{name}:{val}");
            }
            else if (val is IEnumerable enurable)
            {
                sb.AppendLine(ProcessCollectionProperty(prop, this, enurable));
            }
            else
            {
                sb.AppendLine($"{name}:{val}");
            }
        }
        return sb.ToString();
    }

    private string ProcessCollectionProperty(PropertyInfo prop, object newObject, IEnumerable enumerable)
    {
        Type elementType = prop.PropertyType.GetElementType() ??
                           (prop.PropertyType.IsGenericType ? prop.PropertyType.GetGenericArguments()[0] : null);

        if (elementType == null) return string.Empty;

        IList newCollection;
        var sb = new StringBuilder();
        sb.AppendLine($"{prop.Name}");
        if (prop.PropertyType.IsArray)
        {
            // 处理数组
            Array sourceArray = (Array)enumerable;
            for (int i = 0; i < sourceArray.Length; i++)
            {
                object item = sourceArray.GetValue(i);
                sb.AppendLine(item.ToString());
            }
        }
        else if (typeof(IDictionary).IsAssignableFrom(prop.PropertyType))
        {
            IDictionary sourceDict = (IDictionary)enumerable;

            foreach (DictionaryEntry entry in sourceDict)
            {
                sb.AppendLine(entry.Key.ToString());
                sb.AppendLine(entry.Value.ToString());
            }
        }
        else if (typeof(IList).IsAssignableFrom(prop.PropertyType))
        {
            foreach (object item in enumerable)
            {
                sb.AppendLine(item.ToString());
            }
        }
        return sb.ToString();
    }
    
}

public enum DownloadStationMothed
{
    //info
    getinfo,
    getconfig,
    setserverconfig,

    //schedle
    //getconfig,
    setconfig,

    //Task
    //getinfo,
    pause,
    resume,
    edit,
    list,
    create,
    delete

    //statistic
    //getinfo,
}