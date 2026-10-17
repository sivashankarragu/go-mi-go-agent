using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using GoMigo.Application.Research;
using GoMigo.Domain.Discovery;
using GoMigo.Domain.Research;
using Npgsql;

namespace GoMigo.Infrastructure.Persistence.ResearchRuns;

/// <summary>Dapper/PostgreSQL repository. All timestamps are stored as UTC <c>timestamptz</c>.</summary>
public sealed class PostgresResearchRunRepository(NpgsqlDataSource dataSource) : IResearchRunRepository
{
    private static readonly JsonSerializerOptions RequestJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task AddAsync(ResearchRun run, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(ResearchRunSql.InsertRun, new
        {
            run.Id,
            Status = run.Status.ToString(),
            run.Attempt,
            run.Request.LocationText,
            Request = JsonSerializer.Serialize(run.Request, RequestJson),
            CreatedAt = run.CreatedAtUtc.UtcDateTime,
            StartedAt = run.StartedAtUtc?.UtcDateTime,
            CompletedAt = run.CompletedAtUtc?.UtcDateTime,
            ErrorCode = run.Error?.Code,
            ErrorMessage = run.Error?.Message,
            UpdatedAt = DateTime.UtcNow
        }, transaction, cancellationToken: cancellationToken));
        await InsertChildrenAsync(connection, transaction, run, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveAsync(ResearchRun run, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var updated = await connection.ExecuteAsync(new CommandDefinition(ResearchRunSql.UpdateRun, new
        {
            run.Id,
            Status = run.Status.ToString(),
            run.Attempt,
            StartedAt = run.StartedAtUtc?.UtcDateTime,
            CompletedAt = run.CompletedAtUtc?.UtcDateTime,
            ErrorCode = run.Error?.Code,
            ErrorMessage = run.Error?.Message,
            AgentSessions = JsonSerializer.Serialize(run.AgentSessions, RequestJson),
            UpdatedAt = DateTime.UtcNow
        }, transaction, cancellationToken: cancellationToken));
        if (updated == 0)
        {
            throw new InvalidOperationException($"Research run {run.Id} does not exist.");
        }
        await InsertChildrenAsync(connection, transaction, run, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ResearchRun?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var grid = await connection.QueryMultipleAsync(
            new CommandDefinition(ResearchRunSql.SelectRunWithDetails, new { Id = id }, cancellationToken: cancellationToken));

        var row = await grid.ReadSingleOrDefaultAsync<RunRow>();
        if (row is null)
        {
            return null;
        }
        var events = await grid.ReadAsync<ProgressRow>();
        var candidates = await grid.ReadAsync<CandidateRow>();

        var request = JsonSerializer.Deserialize<DiscoveryRequest>(row.Request, RequestJson)
                      ?? throw new InvalidOperationException($"Research run {id} has an unreadable request.");

        return ResearchRun.Restore(
            row.Id,
            request,
            Enum.Parse<ResearchRunStatus>(row.Status),
            row.Attempt,
            Utc(row.CreatedAt),
            row.StartedAt is null ? null : Utc(row.StartedAt.Value),
            row.CompletedAt is null ? null : Utc(row.CompletedAt.Value),
            row.ErrorCode is null ? null : new ResearchError(row.ErrorCode, row.ErrorMessage ?? string.Empty),
            events.Select(e => new ResearchProgressEvent(e.Id, e.Attempt, e.Stage, e.Message, Utc(e.OccurredAt))),
            candidates.Select(ToCandidate),
            string.IsNullOrEmpty(row.AgentSessions) ? [] : JsonSerializer.Deserialize<List<AgentSession>>(row.AgentSessions, RequestJson) ?? []);
    }

    public async Task<IReadOnlyList<Guid>> GetIdsByStatusAsync(ResearchRunStatus status, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var ids = await connection.QueryAsync<Guid>(
            new CommandDefinition(ResearchRunSql.SelectIdsByStatus, new { Status = status.ToString() }, cancellationToken: cancellationToken));
        return ids.ToList();
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: cancellationToken)) == 1;
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            return false;
        }
    }

    private static async Task InsertChildrenAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ResearchRun run, CancellationToken ct)
    {
        if (run.ProgressEvents.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(ResearchRunSql.InsertProgressEvent,
                run.ProgressEvents.Select(e => new
                {
                    e.Id,
                    RunId = run.Id,
                    e.Attempt,
                    e.Stage,
                    e.Message,
                    OccurredAt = e.OccurredAtUtc.UtcDateTime
                }), transaction, cancellationToken: ct));
        }

        if (run.Candidates.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(ResearchRunSql.InsertCandidate,
                run.Candidates.Select(c => new
                {
                    c.Id,
                    RunId = run.Id,
                    run.Attempt,
                    c.DedupeKey,
                    c.Name,
                    Category = c.Category.ToString(),
                    c.Address,
                    c.Neighborhood,
                    c.Latitude,
                    c.Longitude,
                    c.Description,
                    c.SourceName,
                    c.SourceUrl,
                    c.OpeningHours,
                    c.EstimatedCostPerPerson,
                    c.Currency,
                    c.SuggestedVisitMinutes,
                    c.Evidence,
                    LastCheckedAt = c.LastCheckedUtc.UtcDateTime,
                    VerificationStatus = c.VerificationStatus.ToString(),
                    Warnings = c.Warnings.ToArray(),
                    MissingFields = c.MissingFields.ToArray(),
                    RankScore = (decimal)c.RankScore
                }), transaction, cancellationToken: ct));
        }
    }

    private static VenueCandidate ToCandidate(CandidateRow c) => new()
    {
        Id = c.Id,
        DedupeKey = c.DedupeKey,
        Name = c.Name,
        Category = Enum.TryParse<ActivityCategory>(c.Category, out var category) ? category : ActivityCategory.Other,
        Address = c.Address,
        Neighborhood = c.Neighborhood,
        Latitude = c.Latitude,
        Longitude = c.Longitude,
        Description = c.Description,
        SourceName = c.SourceName,
        SourceUrl = c.SourceUrl,
        OpeningHours = c.OpeningHours,
        EstimatedCostPerPerson = c.EstimatedCostPerPerson,
        Currency = c.Currency,
        SuggestedVisitMinutes = c.SuggestedVisitMinutes,
        Evidence = c.Evidence,
        LastCheckedUtc = Utc(c.LastCheckedAt),
        VerificationStatus = Enum.TryParse<VerificationStatus>(c.VerificationStatus, out var vs) ? vs : VerificationStatus.Unverified,
        Warnings = c.Warnings ?? [],
        MissingFields = c.MissingFields ?? [],
        RankScore = (double)c.RankScore
    };

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    // Row types mapped by Dapper (snake_case columns → PascalCase via MatchNamesWithUnderscores).
    private sealed class RunRow
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public int Attempt { get; set; }
        public string Request { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? AgentSessions { get; set; }
    }

    private sealed class ProgressRow
    {
        public Guid Id { get; set; }
        public int Attempt { get; set; }
        public string Stage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
    }

    private sealed class CandidateRow
    {
        public Guid Id { get; set; }
        public string DedupeKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Neighborhood { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Description { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string? SourceUrl { get; set; }
        public string? OpeningHours { get; set; }
        public decimal? EstimatedCostPerPerson { get; set; }
        public string? Currency { get; set; }
        public int? SuggestedVisitMinutes { get; set; }
        public string? Evidence { get; set; }
        public DateTime LastCheckedAt { get; set; }
        public string VerificationStatus { get; set; } = string.Empty;
        public string[]? Warnings { get; set; }
        public string[]? MissingFields { get; set; }
        public decimal RankScore { get; set; }
    }
}
