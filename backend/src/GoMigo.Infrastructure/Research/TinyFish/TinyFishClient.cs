using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace GoMigo.Infrastructure.Research.TinyFish;

// Contracts per https://docs.tinyfish.ai/agent-api/reference (verified 2026-10).
// Only the fields GoMigo uses are modelled; unknown fields are ignored.

public sealed record TinyFishRunRequest(string Url, string Goal, TinyFishAgentConfig? AgentConfig);

public sealed record TinyFishAgentConfig(int? MaxSteps, int? MaxDurationSeconds);

public sealed record TinyFishStartResponse(string? RunId, TinyFishError? Error);

public sealed record TinyFishRun(
    string? RunId,
    string? Status,
    JsonElement? Result,
    TinyFishError? Error,
    string? StreamingUrl,
    IReadOnlyList<TinyFishStep>? Steps);

/// <summary>One agent step as returned on the run object (action text, timing, screenshot link).</summary>
public sealed record TinyFishStep(string? Id, string? Timestamp, string? Status, string? Action, string? Screenshot, string? Duration);

public sealed record TinyFishError(string? Code, string? Message, string? Category, double? RetryAfter);

public static class TinyFishRunStatus
{
    public const string Pending = "PENDING";
    public const string Running = "RUNNING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";

    public static bool IsTerminal(string? status) => status is Completed or Failed or Cancelled;
}

/// <summary>A TinyFish call failed. <see cref="Exception.Message"/> is safe to show; <see cref="IsFatal"/> means retrying other sources is pointless.</summary>
public sealed class TinyFishApiException(string code, string safeMessage, bool isFatal, Exception? inner = null)
    : Exception(safeMessage, inner)
{
    public string Code { get; } = code;
    public bool IsFatal { get; } = isFatal;
}

/// <summary>Thin typed client for the TinyFish Web Agent REST API. The API key is attached here and never logged.</summary>
public sealed class TinyFishClient(HttpClient httpClient, ILogger<TinyFishClient> logger)
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<string> StartRunAsync(TinyFishRunRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => httpClient.PostAsJsonAsync("v1/automation/run-async", request, JsonOptions, cancellationToken),
            cancellationToken);
        var body = await ReadAsync<TinyFishStartResponse>(response, cancellationToken);

        if (body?.Error is not null || string.IsNullOrWhiteSpace(body?.RunId))
        {
            logger.LogWarning("TinyFish refused to start a run: {Code} {Message}", body?.Error?.Code, body?.Error?.Message);
            throw new TinyFishApiException(body?.Error?.Code ?? "StartFailed", "The research agent could not start.", isFatal: false);
        }
        return body.RunId;
    }

    public async Task<TinyFishRun> GetRunAsync(string runId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => httpClient.GetAsync($"v1/runs/{Uri.EscapeDataString(runId)}", cancellationToken),
            cancellationToken);
        return await ReadAsync<TinyFishRun>(response, cancellationToken)
               ?? throw new TinyFishApiException("InvalidResponse", "The research agent returned an invalid response.", isFatal: false);
    }

    /// <summary>Screenshot of one step (<c>GET /v1/runs/{run}/steps/{step}/screenshot</c>); null if unavailable.</summary>
    public async Task<(byte[] Content, string ContentType)?> GetStepScreenshotAsync(string runId, string stepId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"v1/runs/{Uri.EscapeDataString(runId)}/steps/{Uri.EscapeDataString(stepId)}/screenshot", cancellationToken);
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (!response.IsSuccessStatusCode || contentType is null || !contentType.StartsWith("image/", StringComparison.Ordinal))
            {
                logger.LogInformation("No screenshot for TinyFish step {StepId}: HTTP {StatusCode}", stepId, (int)response.StatusCode);
                return null;
            }
            return (await response.Content.ReadAsByteArrayAsync(cancellationToken), contentType);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Could not fetch screenshot for TinyFish step {StepId}", stepId);
            return null;
        }
    }

    /// <summary>Best effort: failures are logged and ignored.</summary>
    public async Task TryCancelRunAsync(string runId)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var response = await httpClient.PostAsync($"v1/runs/{Uri.EscapeDataString(runId)}/cancel", null, cts.Token);
            logger.LogInformation("Requested cancellation of TinyFish run {TinyFishRunId}: {StatusCode}", runId, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not cancel TinyFish run {TinyFishRunId}", runId);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TinyFishApiException("Timeout", "The research service did not respond in time.", isFatal: false, ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Network error calling TinyFish");
            throw new TinyFishApiException("NetworkError", "The research service could not be reached.", isFatal: false, ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var status = response.StatusCode;
        var errorBody = await ReadErrorBodyAsync(response);
        logger.LogWarning("TinyFish returned HTTP {StatusCode}: {ErrorBody}", (int)status, errorBody);
        response.Dispose();
        throw status switch
        {
            HttpStatusCode.Unauthorized =>
                new TinyFishApiException("ProviderAuthFailed", "The research service rejected GoMigo's credentials.", isFatal: true),
            HttpStatusCode.Forbidden =>
                new TinyFishApiException("ProviderForbidden", "The research service refused this request for GoMigo's account.", isFatal: true),
            HttpStatusCode.TooManyRequests =>
                new TinyFishApiException("ProviderRateLimited", "The research service is busy. Please retry shortly.", isFatal: true),
            HttpStatusCode.BadRequest =>
                new TinyFishApiException("ProviderRejectedRequest", "The research service rejected the request.", isFatal: false),
            _ => new TinyFishApiException("ProviderUnavailable", "The research service is temporarily unavailable.", isFatal: false)
        };
    }

    /// <summary>TinyFish error bodies explain refusals (plan limits, invalid input). Logged only, truncated.</summary>
    private static async Task<string> ReadErrorBodyAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            return body.Length <= 500 ? body : body[..500] + "…";
        }
        catch (Exception)
        {
            return "<unreadable>";
        }
    }

    private async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "TinyFish returned malformed JSON");
            throw new TinyFishApiException("InvalidResponse", "The research agent returned an invalid response.", isFatal: false, ex);
        }
    }
}
