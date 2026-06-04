using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SportsMax.Models;

namespace SportsMax.Services;

public interface IEventSource
{
    string SourceName { get; }
    Task<List<SportEvent>> FetchAsync(HttpClient http, CancellationToken ct);
}
