using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SportsMax.Models;

namespace SportsMax.Services;

/// <summary>
/// Fuente con array plano: [{category, link, title, time, status, language, date}]
/// </summary>
public class La14HdSource : IEventSource
{
    private readonly string _url;
    public string SourceName { get; }

    public La14HdSource(string sourceName, string url)
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

            // Si el endpoint devuelve HTML (algunos hosts mueven la ruta) no es error fatal
            var trimmed = json.TrimStart();
            if (!trimmed.StartsWith("[") && !trimmed.StartsWith("{"))
            {
                App.Log($"[La14HdSource:{SourceName}] respuesta no JSON, omitida");
                return result;
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            // Agrupamos por (title + date + time) para acumular varios canales por evento
            var grouped = new Dictionary<string, SportEvent>(StringComparer.OrdinalIgnoreCase);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var title = ReadStr(el, "title");
                var time = ReadStr(el, "time");
                var date = ReadStr(el, "date");
                var link = ReadStr(el, "link");
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
                    continue;

                var key = $"{title}|{date}|{time}";
                if (!grouped.TryGetValue(key, out var ev))
                {
                    ev = new SportEvent
                    {
                        Title = SanitizeUtf8(title),
                        Category = SanitizeUtf8(ReadStr(el, "category")),
                        Time = time,
                        Date = date,
                        Language = SanitizeUtf8(ReadStr(el, "language")),
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
            App.Log($"[La14HdSource:{SourceName}] {ex.Message}");
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

    private static string SanitizeUtf8(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // Algunas fuentes regresan texto mal codificado (UTF-8 leido como latin1)
        try
        {
            var bytes = System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(s);
            var fixedStr = System.Text.Encoding.UTF8.GetString(bytes);
            if (fixedStr.Contains('�')) return s;
            // Heuristica: si el original contiene la secuencia 'Ã' es probable mal encode
            if (s.Contains('Ã') || s.Contains("Â")) return fixedStr;
        }
        catch { /* ignore */ }
        return s;
    }
}
