-- GoMigo 0003: normalized venue candidates per run attempt.
-- NULL means "unknown": opening hours, prices and durations are never filled in by GoMigo.

CREATE TABLE IF NOT EXISTS venue_candidates (
    id                         uuid           PRIMARY KEY,
    run_id                     uuid           NOT NULL REFERENCES research_runs (id) ON DELETE CASCADE,
    attempt                    integer        NOT NULL CHECK (attempt >= 1),
    dedupe_key                 text           NOT NULL,
    name                       text           NOT NULL,
    category                   text           NOT NULL,
    address                    text           NULL,
    neighborhood               text           NULL,
    latitude                   double precision NULL CHECK (latitude BETWEEN -90 AND 90),
    longitude                  double precision NULL CHECK (longitude BETWEEN -180 AND 180),
    description                text           NULL,
    source_name                text           NOT NULL,
    source_url                 text           NULL,
    opening_hours              text           NULL,
    estimated_cost_per_person  numeric(12, 2) NULL CHECK (estimated_cost_per_person >= 0),
    currency                   char(3)        NULL,
    suggested_visit_minutes    integer        NULL CHECK (suggested_visit_minutes > 0),
    evidence                   text           NULL,
    last_checked_at            timestamptz    NOT NULL,
    verification_status        text           NOT NULL
                               CHECK (verification_status IN ('Verified', 'PartiallyVerified', 'Unverified')),
    warnings                   text[]         NOT NULL DEFAULT '{}',
    missing_fields             text[]         NOT NULL DEFAULT '{}',
    rank_score                 numeric(6, 2)  NOT NULL DEFAULT 0,
    created_at                 timestamptz    NOT NULL DEFAULT now(),

    -- The same venue is stored at most once per run attempt; duplicates are ignored on insert.
    CONSTRAINT uq_venue_candidates_run_attempt_dedupe UNIQUE (run_id, attempt, dedupe_key)
);

CREATE INDEX IF NOT EXISTS ix_venue_candidates_run_attempt_score
    ON venue_candidates (run_id, attempt, rank_score DESC);
