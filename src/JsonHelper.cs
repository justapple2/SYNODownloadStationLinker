using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace SYNODownloadStationLinker;

public class JsonHelper
{
    // 默认的 JSON 序列化选项
    private static readonly JsonSerializerOptions _defaultOptions = new JsonSerializerOptions
    {
        WriteIndented = true, // 格式化输出（美化 JSON）
        PropertyNameCaseInsensitive = true, // 属性名称不区分大小写
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };


    public static string Serialize<T>(T obj, bool indented = false)
    {
        var options = indented ? new JsonSerializerOptions(_defaultOptions) { WriteIndented = true } : _defaultOptions;
        return JsonSerializer.Serialize(obj, options);
    }

    public static T Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, _defaultOptions);
        }
        catch
        {
            return default;
        }
    }


    public static bool TryDeserialize<T>(string json, out T result)
    {
        try
        {
            result = Deserialize<T>(json);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }


    public static string Format(string json)
    {
        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
            return JsonSerializer.Serialize(jsonElement, _defaultOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("格式化 JSON 字符串失败，请检查输入内容。", ex);
        }
    }

 
    public static bool IsValid(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            JsonSerializer.Deserialize<JsonElement>(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static T DeepClone<T>(T obj)
    {
        var jsonData = Serialize(obj);
        return Deserialize<T>(jsonData);
    }
}