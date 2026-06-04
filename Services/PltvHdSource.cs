using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SportsMax.Models;

namespace SportsMax.Services;

/// <summary>
/// Strapi-like: {data:[{attributes:{diary_description,diary_hour,date_diary,
///   embeds:{data:[{attributes:{embed_name, embed_iframe}}]},
///   country:{data:{attributes:{name}}}
/// }}]}
/// Las URLs estan en embed_iframe; el querystring r= viene en base64.
/// </summary>
public class PltvHdSource : IEventSource
{
    private readonly string _url;
    private readonly string _baseHost;
    public string SourceName { get; }

    public PltvHdSource(string sourceName, string url)
    {
        SourceName = sourceName;
        _url = url;
        _baseHost = new Uri(url).GetLeftPart(UriPartial.Authority);
    }

    public async Task<List<SportEvent>> FetchAsync(HttpClient http, CancellationToken ct)
    {
        var result = new List<SportEvent>();
        try
        {
            var json = await http.GetStringAsync(_url, ct);
            if (string.IsNullOrWhiteSpace(json)) return result;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in data.EnumerateArray())
            {
                if (!item.TryGetProperty("attributes", out var attr)) continue;

                var title = ReadStr(attr, "diary_description").Replace("\n", " ").Trim();
                var hour = ReadStr(attr, "diary_hour");
                var date = ReadStr(attr, "date_diary");
                if (string.IsNullOrWhiteSpace(title)) continue;

                var category = string.Empty;
                if (attr.TryGetProperty("country", out var country) &&
                    country.TryGetProperty("data", out var cd) &&
                    cd.ValueKind == JsonValueKind.Object &&
                    cd.TryGetProperty("attributes", out var ca))
                {
                    category = ReadStr(ca, "name");
                }

                var ev = new SportEvent
                {
                    Title = title,
                    Category = string.IsNullOrEmpty(category) ? "Deportes" : category,
                    Time = hour.Length >= 5 ? hour.Substring(0, 5) : hour,
                    Date = date,
                    StatusRaw = "Programado",
                    Source = SourceName
                };

                if (attr.TryGetProperty("embeds", out var embeds) &&
                    embeds.TryGetProperty("data", out var embedData) &&
                    embedData.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in embedData.EnumerateArray())
                    {
                        if (!e.TryGetProperty("attributes", out var ea)) continue;
                        var name = ReadStr(ea, "embed_name");
                        var iframe = ReadStr(ea, "embed_iframe");
                        var resolved = ResolveIframeUrl(iframe);
                        if (string.IsNullOrWhiteSpace(resolved)) continue;
                        ev.Links.Add(new StreamLink
                        {
                            Name = string.IsNullOrWhiteSpace(name) ? "Canal" : name,
                            Url = resolved
                        });
                    }
                }

                if (ev.Links.Count > 0)
                    result.Add(ev);
            }
        }
        catch (Exception ex)
        {
            App.Log($"[PltvHdSource:{SourceName}] {ex.Message}");
        }
        return result;
    }

    private string ResolveIframeUrl(string iframe)
    {
        if (string.IsNullOrWhiteSpace(iframe)) return string.Empty;
        try
        {
            // Si la URL embed contiene ?r=BASE64 devolvemos el destino real
            var full = iframe.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? iframe
                : _baseHost + iframe;

            var uri = new Uri(full);
            var q = UrlUtils.ParseQuery(uri.Query);
            if (q.TryGetValue("r", out var r) && !string.IsNullOrEmpty(r))
            {
                try
                {
                    var bytes = Convert.FromBase64String(r);
                    var decoded = Encoding.UTF8.GetString(bytes);
                    if (decoded.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        return decoded;
                }
                catch { /* devolvemos full a continuacion */ }
            }
            return full;
        }
        catch
        {
            return iframe;
        }
    }

    private static string ReadStr(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var v))
        {
            if (v.ValueKind == JsonValueKind.String) return v.GetString() ?? string.Empty;
            if (v.ValueKind == JsonValueKind.Number) return v.ToString();
        }
        return string.Empty;
    }
}
