-- GoMigo 0001: research runs (one row per discovery request execution).
-- Forward-only, non-destructive. Applied by database/migrate.ps1 inside a transaction.

CREATE TABLE IF NOT EXISTS research_runs (
    id              uuid         PRIMARY KEY,
    status          text         NOT NULL
                    CHECK (status IN ('Pending', 'Running', 'Completed', 'PartiallyCompleted', 'Failed')),
    attempt         integer      NOT NULL DEFAULT 0 CHECK (attempt >= 0),
    location_text   text         NOT NULL,
    request         jsonb        NOT NULL,
    created_at      timestamptz  NOT NULL,
    started_at      timestamptz  NULL,
    completed_at    timestamptz  NULL,
    error_code      text         NULL,
    error_message   text         NULL,
    updated_at      timestamptz  NOT NULL DEFAULT now()
);

-- Startup recovery looks up runs by status; listing is newest-first.
CREATE INDEX IF NOT EXISTS ix_research_runs_status_created_at ON research_runs (status, created_at);
CREATE INDEX IF NOT EXISTS ix_research_runs_created_at ON research_runs (created_at DESC);
