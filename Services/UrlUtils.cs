using System;
using System.Collections.Generic;

namespace SportsMax.Services;

internal static class UrlUtils
{
    /// <summary>
    /// Parser simple de query strings (no requiere System.Web).
    /// </summary>
    public static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return result;
        if (query.StartsWith("?")) query = query.Substring(1);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            string key, value;
            if (idx < 0)
            {
                key = Uri.UnescapeDataString(pair);
                value = string.Empty;
            }
            else
            {
                key = Uri.UnescapeDataString(pair.Substring(0, idx));
                value = Uri.UnescapeDataString(pair.Substring(idx + 1));
            }
            result[key] = value;
        }
        return result;
    }
}
