using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SportsMax.Models;

namespace SportsMax.Services;

/// <summary>
/// Feed anidado por dia:
/// {dias:[{fecha_iso, titulo, eventos:[{titulo, categoria, clase, hora,
///   duracion_min, canales:[{nombre, calidad, url}]}]}]}
///
/// Los canales apuntan a un wrapper (live1.php) que SOLO sirve el reproductor
/// cuando se carga dentro de un &lt;iframe&gt; (verifica Sec-Fetch-Dest). Si se
/// navega directo devuelve "Usa iframe para cargar este reproductor". Por eso
/// estas URLs se reproducen incrustadas en un iframe — ver <see cref="PlayerEmbed"/>.
/// </summary>
public class StreamHdxSource : IEventSource
{
    private readonly string _url;
    public string SourceName { get; }

    public StreamHdxSource(string sourceName, string url)
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
            if (string.IsNullOrWhiteSpace(json)) return result;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("dias", out var dias) ||
                dias.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var dia in dias.EnumerateArray())
            {
                var date = ReadStr(dia, "fecha_iso");
                if (!dia.TryGetProperty("eventos", out var eventos) ||
                    eventos.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var el in eventos.EnumerateArray())
                {
                    var title = SanitizeUtf8(ReadStr(el, "titulo"));
                    var time = ReadStr(el, "hora");
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    if (!el.TryGetProperty("canales", out var canales) ||
                        canales.ValueKind != JsonValueKind.Array)
                        continue;

                    var ev = new SportEvent
                    {
                        Title = title,
                        Category = Capitalize(SanitizeUtf8(ReadStr(el, "categoria"))),
                        Time = time,
                        Date = date,
                        StatusRaw = "Programado",
                        Source = SourceName
                    };

                    foreach (var c in canales.EnumerateArray())
                    {
                        var url = ReadStr(c, "url");
                        if (string.IsNullOrWhiteSpace(url)) continue;

                        var name = SanitizeUtf8(ReadStr(c, "nombre"));
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            var quality = ReadStr(c, "calidad");
                            name = string.IsNullOrWhiteSpace(quality) ? "Canal" : quality;
                        }
                        ev.Links.Add(new StreamLink { Name = name, Url = url });
                    }

                    if (ev.Links.Count > 0)
                        result.Add(ev);
                }
            }
        }
        catch (Exception ex)
        {
            App.Log($"[StreamHdxSource:{SourceName}] {ex.Message}");
        }
        return result;
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

    private static string Capitalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
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
            if (s.Contains('Ã') || s.Contains('Â')) return fixedStr;
        }
        catch { /* ignore */ }
        return s;
    }
}
