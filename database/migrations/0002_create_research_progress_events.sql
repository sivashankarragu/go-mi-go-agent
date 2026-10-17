-- GoMigo 0002: progress events shown to the user while a run executes.

CREATE TABLE IF NOT EXISTS research_progress_events (
    id           uuid         PRIMARY KEY,
    run_id       uuid         NOT NULL REFERENCES research_runs (id) ON DELETE CASCADE,
    attempt      integer      NOT NULL CHECK (attempt >= 0),
    stage        text         NOT NULL,
    message      text         NOT NULL,
    occurred_at  timestamptz  NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_research_progress_events_run_id_occurred_at
    ON research_progress_events (run_id, occurred_at);
