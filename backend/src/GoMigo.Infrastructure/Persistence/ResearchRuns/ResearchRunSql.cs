namespace GoMigo.Infrastructure.Persistence.ResearchRuns;

/// <summary>Parameterized SQL for the research-runs module. Schema: database/migrations/0001–0003.</summary>
internal static class ResearchRunSql
{
    public const string InsertRun = """
        INSERT INTO research_runs
            (id, status, attempt, location_text, request, created_at, started_at, completed_at, error_code, error_message, updated_at)
        VALUES
            (@Id, @Status, @Attempt, @LocationText, CAST(@Request AS jsonb), @CreatedAt, @StartedAt, @CompletedAt, @ErrorCode, @ErrorMessage, @UpdatedAt);
        """;

    public const string UpdateRun = """
        UPDATE research_runs
        SET status = @Status,
            attempt = @Attempt,
            started_at = @StartedAt,
            completed_at = @CompletedAt,
            error_code = @ErrorCode,
            error_message = @ErrorMessage,
            agent_sessions = CAST(@AgentSessions AS jsonb),
            updated_at = @UpdatedAt
        WHERE id = @Id;
        """;

    public const string InsertProgressEvent = """
        INSERT INTO research_progress_events (id, run_id, attempt, stage, message, occurred_at)
        VALUES (@Id, @RunId, @Attempt, @Stage, @Message, @OccurredAt)
        ON CONFLICT (id) DO NOTHING;
        """;

    /// <summary>Duplicate venues within one attempt are ignored rather than failing the write.</summary>
    public const string InsertCandidate = """
        INSERT INTO venue_candidates
            (id, run_id, attempt, dedupe_key, name, category, address, neighborhood, latitude, longitude, description,
             source_name, source_url, opening_hours, estimated_cost_per_person, currency, suggested_visit_minutes,
             evidence, last_checked_at, verification_status, warnings, missing_fields, rank_score)
        VALUES
            (@Id, @RunId, @Attempt, @DedupeKey, @Name, @Category, @Address, @Neighborhood, @Latitude, @Longitude, @Description,
             @SourceName, @SourceUrl, @OpeningHours, @EstimatedCostPerPerson, @Currency, @SuggestedVisitMinutes,
             @Evidence, @LastCheckedAt, @VerificationStatus, @Warnings, @MissingFields, @RankScore)
        ON CONFLICT (run_id, attempt, dedupe_key) DO NOTHING;
        """;

    public const string SelectRunWithDetails = """
        SELECT id, status, attempt, request::text AS request, created_at, started_at, completed_at, error_code, error_message,
               agent_sessions::text AS agent_sessions
        FROM research_runs
        WHERE id = @Id;

        SELECT id, attempt, stage, message, occurred_at
        FROM research_progress_events
        WHERE run_id = @Id
        ORDER BY occurred_at, id;

        SELECT c.id, c.dedupe_key, c.name, c.category, c.address, c.neighborhood, c.latitude, c.longitude, c.description,
               c.source_name, c.source_url, c.opening_hours, c.estimated_cost_per_person, c.currency, c.suggested_visit_minutes,
               c.evidence, c.last_checked_at, c.verification_status, c.warnings, c.missing_fields, c.rank_score
        FROM venue_candidates c
        JOIN research_runs r ON r.id = c.run_id AND r.attempt = c.attempt
        WHERE c.run_id = @Id
        ORDER BY c.rank_score DESC, c.name;
        """;

    public const string SelectIdsByStatus = """
        SELECT id FROM research_runs WHERE status = @Status ORDER BY created_at;
        """;
}
