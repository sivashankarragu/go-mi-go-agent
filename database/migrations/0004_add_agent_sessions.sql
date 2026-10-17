-- GoMigo 0004: web-agent activity per run (start page, live view URL, steps) for the current attempt.
-- Forward-only, non-destructive: adds a column with a default; existing rows get an empty list.

ALTER TABLE research_runs
    ADD COLUMN IF NOT EXISTS agent_sessions jsonb NOT NULL DEFAULT '[]'::jsonb;
