using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace SYNODownloadStationLinker;

public static class JsonHelper
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public static string Serialize<T>(T obj, bool indented = false)
    {
        var options = indented ? new JsonSerializerOptions(DefaultOptions) { WriteIndented = true } : DefaultOptions;
        return JsonSerializer.Serialize(obj, options);
    }

    public static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, DefaultOptions);
        }
        catch
        {
            return default;
        }
    }

    public static bool TryDeserialize<T>(string json, out T? result)
    {
        result = Deserialize<T>(json);
        return result is not null;
    }

    public static string Format(string json)
    {
        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
            return JsonSerializer.Serialize(jsonElement, DefaultOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("格式化 JSON 失败，请检查输入内容。", ex);
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

    public static T? DeepClone<T>(T obj)
    {
        var jsonData = Serialize(obj);
        return Deserialize<T>(jsonData);
    }
}

