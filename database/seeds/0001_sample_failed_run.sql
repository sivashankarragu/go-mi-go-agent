-- Optional demo data (apply with: migrate.ps1 -WithSeeds).
-- A failed run you can use to try the retry endpoint:
--   POST /api/discovery/research/00000000-0000-0000-0000-00000000a001/retry

INSERT INTO research_runs (id, status, attempt, location_text, request, created_at, started_at, completed_at, error_code, error_message, updated_at)
VALUES (
    '00000000-0000-0000-0000-00000000a001',
    'Failed',
    1,
    'Indiranagar, Bengaluru',
    '{"locationText":"Indiranagar, Bengaluru","latitude":null,"longitude":null,"durationMinutes":180,"budgetPerPerson":700,"currency":"INR","category":"Fun","startTime":null,"additionalPreferences":null}'::jsonb,
    now() - interval '10 minutes',
    now() - interval '10 minutes',
    now() - interval '9 minutes',
    'ProviderUnavailable',
    'The research service is currently unavailable. Please retry.',
    now()
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO research_progress_events (id, run_id, attempt, stage, message, occurred_at)
VALUES
    ('00000000-0000-0000-0000-00000000b001', '00000000-0000-0000-0000-00000000a001', 0, 'queued', 'Research request received.', now() - interval '10 minutes'),
    ('00000000-0000-0000-0000-00000000b002', '00000000-0000-0000-0000-00000000a001', 1, 'failed', 'The research service is currently unavailable. Please retry.', now() - interval '9 minutes')
ON CONFLICT (id) DO NOTHING;
