using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SportsMax.Models;

namespace SportsMax.Services;

/// <summary>
/// Misma forma que La14Hd pero con codificacion limpia: [{title, time, category, status, link, language}]
/// </summary>
public class StreamTpSource : IEventSource
{
    private readonly string _url;
    public string SourceName { get; }

    public StreamTpSource(string sourceName, string url)
    {
        SourceName = sourceName;
        _url = url;
    }

    public async Task<List<SportEvent>> FetchAsync(HttpClient http, CancellationToken ct)
    {
        var result = new List<SportEvent>();
        try
        {
            var json = await http.GetStringAsync(_url, ct);
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]")
                return result;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var grouped = new Dictionary<string, SportEvent>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var title = ReadStr(el, "title");
                var time = ReadStr(el, "time");
                var link = ReadStr(el, "link");
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
                    continue;

                var key = $"{title}|{time}";
                if (!grouped.TryGetValue(key, out var ev))
                {
                    ev = new SportEvent
                    {
                        Title = title,
                        Category = ReadStr(el, "category"),
                        Time = time,
                        Date = today,
                        Language = ReadStr(el, "language"),
                        StatusRaw = ReadStr(el, "status"),
                        Source = SourceName
                    };
                    grouped[key] = ev;
                    result.Add(ev);
                }

                var name = ExtractChannelName(link);
                ev.Links.Add(new StreamLink { Name = name, Url = link });
            }
        }
        catch (Exception ex)
        {
            App.Log($"[StreamTpSource:{SourceName}] {ex.Message}");
        }
        return result;
    }

    private static string ReadStr(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static string ExtractChannelName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var q = UrlUtils.ParseQuery(uri.Query);
            if (q.TryGetValue("stream", out var s) && !string.IsNullOrWhiteSpace(s)) return s;
            return uri.Host;
        }
        catch
        {
            return "Canal";
        }
    }
}
