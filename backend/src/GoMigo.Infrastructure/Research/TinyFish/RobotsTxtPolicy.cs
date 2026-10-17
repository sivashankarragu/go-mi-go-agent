using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GoMigo.Infrastructure.Research.TinyFish;

/// <summary>
/// Honours each site's robots.txt before an agent is sent to a page found by search, using the rules for
/// all crawlers (<c>User-agent: *</c>). Longest matching Allow/Disallow wins; <c>*</c> and <c>$</c> are supported.
/// Missing robots.txt (4xx) means allowed; an unreachable one (5xx, timeout) means not allowed.
/// </summary>
public sealed class RobotsTxtPolicy(HttpClient httpClient, IMemoryCache cache, ILogger<RobotsTxtPolicy> logger)
{
    private sealed record Rule(bool Allow, string Pattern, Regex Regex);

    public async Task<bool> IsAllowedAsync(Uri url, CancellationToken cancellationToken)
    {
        var rules = await GetRulesAsync(url, cancellationToken);
        if (rules is null) return false;

        var path = url.PathAndQuery;
        var match = rules.Where(r => r.Regex.IsMatch(path))
            .OrderByDescending(r => r.Pattern.Length)
            .ThenByDescending(r => r.Allow) // on a tie, Allow wins (Google's rule)
            .FirstOrDefault();
        return match?.Allow ?? true;
    }

    private async Task<IReadOnlyList<Rule>?> GetRulesAsync(Uri url, CancellationToken cancellationToken)
    {
        var origin = $"{url.Scheme}://{url.Authority}";
        return await cache.GetOrCreateAsync($"robots:{origin}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            try
            {
                using var response = await httpClient.GetAsync($"{origin}/robots.txt", cancellationToken);
                if ((int)response.StatusCode is >= 400 and < 500)
                {
                    return (IReadOnlyList<Rule>)[];
                }
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return null;
                }
                return Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(ex, "robots.txt unreachable for {Origin}; treating as not allowed", origin);
                return null;
            }
        });
    }

    /// <summary>Rules from the group(s) that apply to every crawler (<c>User-agent: *</c>).</summary>
    private static IReadOnlyList<Rule> Parse(string robots)
    {
        var rules = new List<Rule>();
        var groupAgents = new List<string>();
        var inRules = false;

        foreach (var rawLine in robots.Split('\n'))
        {
            var line = rawLine.Split('#')[0].Trim();
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var field = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();

            if (field == "user-agent")
            {
                if (inRules) { groupAgents.Clear(); inRules = false; }
                groupAgents.Add(value.ToLowerInvariant());
            }
            else if (field is "allow" or "disallow")
            {
                inRules = true;
                if (!groupAgents.Contains("*") || value.Length == 0) continue;
                rules.Add(new Rule(field == "allow", value, ToRegex(value)));
            }
        }
        return rules;
    }

    private static Regex ToRegex(string pattern)
    {
        var anchored = pattern.EndsWith('$');
        var body = Regex.Escape(anchored ? pattern[..^1] : pattern).Replace(@"\*", ".*");
        return new Regex("^" + body + (anchored ? "$" : ""), RegexOptions.CultureInvariant);
    }
}
