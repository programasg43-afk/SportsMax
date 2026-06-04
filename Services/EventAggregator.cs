using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SportsMax.Models;

namespace SportsMax.Services;

public class EventAggregator
{
    private readonly List<IEventSource> _sources;
    private readonly HttpClient _http;

    public EventAggregator()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/124.0 Safari/537.36 SportsMax/1.0");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json,text/plain,*/*");

        // URLs de las fuentes ofuscadas (XOR + Base64) para no exponerlas en texto
        // plano en el codigo fuente. Se decodifican en runtime con Obfuscator.
        _sources = new List<IEventSource>
        {
            new La14HdSource("La14HD",     Obfuscator.Decode("OwREAgdJYhsUWwtSDQFKWVUbHl9MV15GWSBfWgEbHWJVH19UAgRUVgkUHEJVVA==")),
            new PltvHdSource("PLTVHD",     Obfuscator.Decode("OwREAgdJYhsIVk4QDQFKWVUbHl5TU0JbUyBeWgEbHQ==")),
            new StreamTpSource("StreamTP", Obfuscator.Decode("OwREAgdJYhsLTkgDBAgQSlQTRhRZXV0dUyUVXgYbAGNeC1VU")),
            // tvtvhd publica un agenda123.json con el mismo formato que la14hd
            new La14HdSource("TVTVHD",     Obfuscator.Decode("OwREAgdJYhsMTE4QDQFKWVUbHl9MV15GWSBfWgEbHWJVH19UAgRUVgkUHEJVVA==")),
        };
    }

    public IEnumerable<string> SourceNames => _sources.Select(s => s.SourceName);

    public async Task<List<SportEvent>> FetchAllAsync(
        IProgress<(string source, int count, bool error)>? progress,
        CancellationToken ct)
    {
        var tasks = _sources.Select(async src =>
        {
            try
            {
                var list = await src.FetchAsync(_http, ct);
                progress?.Report((src.SourceName, list.Count, false));
                return list;
            }
            catch (Exception ex)
            {
                App.Log($"[Aggregator:{src.SourceName}] {ex.Message}");
                progress?.Report((src.SourceName, 0, true));
                return new List<SportEvent>();
            }
        }).ToArray();

        var all = await Task.WhenAll(tasks);
        var merged = MergeDuplicates(all.SelectMany(x => x));
        return merged
            .OrderBy(e => e.StartLocal ?? DateTime.MaxValue)
            .ThenBy(e => e.Title)
            .ToList();
    }

    private static List<SportEvent> MergeDuplicates(IEnumerable<SportEvent> events)
    {
        var dict = new Dictionary<string, SportEvent>(StringComparer.OrdinalIgnoreCase);
        foreach (var ev in events)
        {
            var key = $"{Normalize(ev.Title)}|{ev.Date}|{ev.Time}";
            if (!dict.TryGetValue(key, out var existing))
            {
                dict[key] = ev;
                continue;
            }

            foreach (var link in ev.Links)
            {
                if (!existing.Links.Any(l => string.Equals(l.Url, link.Url, StringComparison.OrdinalIgnoreCase)))
                {
                    existing.Links.Add(new StreamLink
                    {
                        Name = $"{link.Name} ({ev.Source})",
                        Url = link.Url
                    });
                }
            }

        }
        return dict.Values.ToList();
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var t = s.ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        foreach (var c in t)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (char.IsWhiteSpace(c)) sb.Append(' ');
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }
}
