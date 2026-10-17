// Typed mirrors of the GoMigo API contracts (backend: GoMigo.Application/Discovery/Contracts).
// `null` always means "unknown" — render it as such, never substitute a guess.

export type ActivityCategory =
  | 'Any'
  | 'Fun'
  | 'Food'
  | 'Culture'
  | 'Outdoors'
  | 'Nightlife'
  | 'Shopping'
  | 'Relaxation'
  | 'Other';

export type ResearchRunStatus = 'Pending' | 'Running' | 'Completed' | 'PartiallyCompleted' | 'Failed';

export type VerificationStatus = 'Verified' | 'PartiallyVerified' | 'Unverified';

export interface DiscoveryRequest {
  locationText: string | null;
  latitude: number | null;
  longitude: number | null;
  durationMinutes: number;
  budgetPerPerson: number;
  currency?: string | null;
  category: ActivityCategory;
  /** ISO 8601 with offset. */
  startTime: string | null;
  additionalPreferences: string | null;
}

export interface StartResearchResponse {
  runId: string;
  status: ResearchRunStatus;
  statusUrl: string;
}

export interface ProgressEvent {
  attempt: number;
  stage: string;
  message: string;
  occurredAtUtc: string;
}

export interface ResearchError {
  code: string;
  message: string;
}

export interface VenueCandidate {
  id: string;
  rank: number;
  rankScore: number;
  /** True when the venue's category is the requested mood (always true for 'Any'). */
  matchesMood: boolean;
  name: string;
  category: ActivityCategory;
  address: string | null;
  neighborhood: string | null;
  latitude: number | null;
  longitude: number | null;
  description: string | null;
  sourceName: string;
  sourceUrl: string | null;
  openingHours: string | null;
  estimatedCostPerPerson: number | null;
  currency: string | null;
  suggestedVisitMinutes: number | null;
  evidence: string | null;
  lastCheckedUtc: string;
  verificationStatus: VerificationStatus;
  warnings: string[];
  missingFields: string[];
}

/** One step a web agent took; screenshotUrl is an API path (prefix with the API base URL). */
export interface AgentStep {
  id: string;
  action: string;
  atUtc: string | null;
  duration: string | null;
  screenshotUrl: string | null;
}

/** What a web agent is doing/did: start page, live browser view while active, and its steps. */
export interface AgentSession {
  providerRunId: string;
  sourceName: string;
  startUrl: string;
  liveViewUrl: string | null;
  status: string;
  isActive: boolean;
  steps: AgentStep[];
  updatedAtUtc: string;
  /** Demo data from the mock provider: no real website was visited. */
  isSimulated: boolean;
}

export interface ResearchRun {
  runId: string;
  status: ResearchRunStatus;
  attempt: number;
  isTerminal: boolean;
  canRetry: boolean;
  request: DiscoveryRequest;
  createdAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  progress: ProgressEvent[];
  candidates: VenueCandidate[];
  agentSessions: AgentSession[];
  error: ResearchError | null;
}

export interface CategoryOption {
  value: ActivityCategory;
  label: string;
  description: string;
}

export interface DiscoveryLimits {
  minDurationMinutes: number;
  maxDurationMinutes: number;
  minBudgetPerPerson: number;
  maxBudgetPerPerson: number;
  maxLocationLength: number;
  maxPreferencesLength: number;
}

export interface DiscoveryOptions {
  categories: CategoryOption[];
  durationPresetsMinutes: number[];
  budgetPresets: number[];
  defaultCurrency: string;
  supportedCurrencies: string[];
  supportedCities: string[];
  limits: DiscoveryLimits;
}

export type AreaMatch = 'City' | 'LocationText' | 'Coordinates';

export interface Curation {
  key: 'hidden-gems' | 'food' | 'outdoors' | 'activities' | string;
  title: string;
  description: string;
  category: ActivityCategory;
  /** Pre-fill for the search form's location field. */
  locationText: string;
}

export interface CurationsResponse {
  area: string;
  city: string;
  matchedBy: AreaMatch;
  distanceKm: number | null;
  curations: Curation[];
}

export type PlaceKind = 'Neighbourhood' | 'Area' | 'Street' | 'Venue';

/** A place from GET /api/places/autocomplete or /api/places/reverse. */
export interface PlaceSuggestion {
  name: string;
  detail: string | null;
  /** Text to put in the location field. */
  label: string;
  latitude: number;
  longitude: number;
  kind: PlaceKind;
  nearestArea: string | null;
}

export interface HealthResponse {
  status: string;
  service: string;
  version: string;
  researchProvider: string;
  persistence: string;
  database: string;
}

/** RFC 7807 problem details returned by the API for every error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
