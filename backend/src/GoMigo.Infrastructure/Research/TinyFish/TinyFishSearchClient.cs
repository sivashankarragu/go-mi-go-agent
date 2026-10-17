using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GoMigo.Infrastructure.Research.TinyFish;

public sealed record SearchHit(int Position, string SiteName, string Title, string Snippet, string Url);

/// <summary>
/// TinyFish Search API (<c>GET https://api.search.tinyfish.ai/?query=…</c>): ranked web results in ~2 s.
/// Free daily allowance, so GoMigo uses it to choose which pages the (paid, slower) agent should read.
/// </summary>
public sealed class TinyFishSearchClient(HttpClient httpClient, ILogger<TinyFishSearchClient> logger)
{
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(
        string query, string purpose, string country, string language, IEnumerable<string> excludeDomains, int page,
        CancellationToken cancellationToken)
    {
        var url = $"?query={Uri.EscapeDataString(query)}&purpose={Uri.EscapeDataString(purpose)}" +
                  $"&location={Uri.EscapeDataString(country)}&language={Uri.EscapeDataString(language)}&page={page}";
        var excluded = string.Join(',', excludeDomains);
        if (excluded.Length > 0) url += $"&exclude_domains={Uri.EscapeDataString(excluded)}";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("TinyFish Search returned HTTP {StatusCode}: {Body}", (int)response.StatusCode, body.Length > 300 ? body[..300] : body);
            return [];
        }

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var hits = new List<SearchHit>();
        foreach (var r in results.EnumerateArray())
        {
            var link = Str(r, "url");
            if (link is null) continue;
            hits.Add(new SearchHit(
                r.TryGetProperty("position", out var p) && p.TryGetInt32(out var pos) ? pos : hits.Count + 1,
                Str(r, "site_name") ?? new Uri(link).Host,
                Str(r, "title") ?? link,
                Str(r, "snippet") ?? string.Empty,
                link));
        }
        return hits;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString() : null;
}
