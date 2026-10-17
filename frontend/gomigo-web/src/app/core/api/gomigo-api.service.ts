import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  CurationsResponse,
  DiscoveryOptions,
  DiscoveryRequest,
  HealthResponse,
  PlaceSuggestion,
  ProblemDetails,
  ResearchRun,
  StartResearchResponse,
} from '../models/discovery.models';

/** Single entry point for the GoMigo backend. Keep UI code (including the Stitch design) on top of this. */
@Injectable({ providedIn: 'root' })
export class GoMigoApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api`;

  getHealth(): Observable<HealthResponse> {
    return this.http.get<HealthResponse>(`${this.baseUrl}/health`);
  }

  getOptions(): Observable<DiscoveryOptions> {
    return this.http.get<DiscoveryOptions>(`${this.baseUrl}/discovery/options`);
  }

  autocompletePlaces(q: string, bias?: { latitude: number; longitude: number } | null): Observable<PlaceSuggestion[]> {
    let params = new HttpParams().set('q', q);
    if (bias) params = params.set('latitude', bias.latitude).set('longitude', bias.longitude);
    return this.http.get<PlaceSuggestion[]>(`${this.baseUrl}/places/autocomplete`, { params });
  }

  reverseGeocode(latitude: number, longitude: number): Observable<PlaceSuggestion> {
    const params = new HttpParams().set('latitude', latitude).set('longitude', longitude);
    return this.http.get<PlaceSuggestion>(`${this.baseUrl}/places/reverse`, { params });
  }

  getCurations(query: { location?: string | null; latitude?: number | null; longitude?: number | null }): Observable<CurationsResponse> {
    let params = new HttpParams();
    if (query.location?.trim()) params = params.set('location', query.location.trim());
    if (query.latitude != null && query.longitude != null) {
      params = params.set('latitude', query.latitude).set('longitude', query.longitude);
    }
    return this.http.get<CurationsResponse>(`${this.baseUrl}/discovery/curations`, { params });
  }

  startResearch(request: DiscoveryRequest): Observable<StartResearchResponse> {
    return this.http.post<StartResearchResponse>(`${this.baseUrl}/discovery/research`, request);
  }

  getResearch(runId: string): Observable<ResearchRun> {
    return this.http.get<ResearchRun>(`${this.baseUrl}/discovery/research/${encodeURIComponent(runId)}`);
  }

  retryResearch(runId: string): Observable<StartResearchResponse> {
    return this.http.post<StartResearchResponse>(
      `${this.baseUrl}/discovery/research/${encodeURIComponent(runId)}/retry`,
      null,
    );
  }
}

export interface ApiErrorView {
  message: string;
  fieldErrors: Record<string, string[]>;
}

/** Converts HTTP failures (including RFC 7807 bodies) into something the UI can show. */
export function toApiError(error: unknown): ApiErrorView {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return { message: 'Cannot reach the GoMigo server. Is the API running?', fieldErrors: {} };
    }
    const problem = (error.error ?? {}) as ProblemDetails;
    return {
      message: problem.detail ?? problem.title ?? `Request failed (${error.status}).`,
      fieldErrors: problem.errors ?? {},
    };
  }
  return { message: 'Something went wrong. Please try again.', fieldErrors: {} };
}
