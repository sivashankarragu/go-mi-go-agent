import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Subscription, retry, switchMap, takeWhile, timer } from 'rxjs';

import { ApiErrorView, GoMigoApiService, toApiError } from '../../core/api/gomigo-api.service';
import { DiscoveryRequest, ResearchRun } from '../../core/models/discovery.models';
import { environment } from '../../../environments/environment';

export type ResearchPhase = 'idle' | 'starting' | 'researching' | 'finished' | 'error';

/**
 * State of one discovery session: starts a research run, polls it until it finishes, supports retry.
 * UI-agnostic on purpose so a new (Stitch) design only needs to bind to these signals.
 * Provide it at component level so polling stops when the page is destroyed.
 */
@Injectable()
export class ResearchSession {
  private readonly api = inject(GoMigoApiService);
  private polling?: Subscription;

  readonly phase = signal<ResearchPhase>('idle');
  readonly run = signal<ResearchRun | null>(null);
  readonly error = signal<ApiErrorView | null>(null);

  readonly candidates = computed(() => this.run()?.candidates ?? []);
  /** Venues in the requested mood; the rest are shown separately as alternatives. */
  readonly exactMatches = computed(() => this.candidates().filter((c) => c.matchesMood));
  readonly otherOptions = computed(() => this.candidates().filter((c) => !c.matchesMood));
  /** Finished, found venues, but none in the requested mood. */
  readonly noExactMatches = computed(
    () => !!this.run()?.isTerminal && this.candidates().length > 0 && this.exactMatches().length === 0,
  );
  readonly progress = computed(() => this.run()?.progress ?? []);
  readonly isBusy = computed(() => this.phase() === 'starting' || this.phase() === 'researching');
  readonly isPartial = computed(() => this.run()?.status === 'PartiallyCompleted');
  readonly isFailed = computed(() => this.run()?.status === 'Failed');
  readonly isEmpty = computed(
    () => this.phase() === 'finished' && this.run()?.status === 'Completed' && this.candidates().length === 0,
  );
  readonly canRetry = computed(() => !this.isBusy() && (this.run()?.canRetry ?? false));

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stopPolling());
  }

  start(request: DiscoveryRequest): void {
    this.stopPolling();
    this.run.set(null);
    this.error.set(null);
    this.phase.set('starting');
    this.api.startResearch(request).subscribe({
      next: (res) => this.watch(res.runId),
      error: (err) => this.fail(err),
    });
  }

  retry(): void {
    const current = this.run();
    if (!current?.canRetry) return;
    this.stopPolling();
    this.error.set(null);
    this.phase.set('starting');
    this.api.retryResearch(current.runId).subscribe({
      next: (res) => this.watch(res.runId),
      error: (err) => this.fail(err),
    });
  }

  reset(): void {
    this.stopPolling();
    this.run.set(null);
    this.error.set(null);
    this.phase.set('idle');
  }

  private watch(runId: string): void {
    this.phase.set('researching');
    this.polling = timer(0, environment.pollIntervalMs)
      .pipe(
        switchMap(() => this.api.getResearch(runId).pipe(retry({ count: 3, delay: 1500 }))),
        takeWhile((run) => !run.isTerminal, true),
      )
      .subscribe({
        next: (run) => {
          this.run.set(run);
          if (run.isTerminal) this.phase.set('finished');
        },
        error: (err) => this.fail(err),
      });
  }

  private fail(err: unknown): void {
    this.error.set(toApiError(err));
    this.phase.set('error');
  }

  private stopPolling(): void {
    this.polling?.unsubscribe();
    this.polling = undefined;
  }
}
