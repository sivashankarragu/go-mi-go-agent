import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { GoMigoApiService } from '../api/gomigo-api.service';
import { HealthResponse } from '../models/discovery.models';

type AgentState = 'checking' | 'ready' | 'demo' | 'offline';

@Component({
  selector: 'app-header',
  imports: [RouterLink],
  templateUrl: './app-header.html',
})
export class AppHeader implements OnInit {
  private readonly api = inject(GoMigoApiService);
  private readonly health = signal<HealthResponse | null>(null);
  private readonly failed = signal(false);

  protected readonly agentState = computed<AgentState>(() => {
    if (this.failed()) return 'offline';
    const h = this.health();
    if (!h) return 'checking';
    return h.researchProvider === 'TinyFish' ? 'ready' : 'demo';
  });

  protected readonly agentLabel = computed(
    () =>
      ({ checking: 'Connecting…', ready: 'Live research', demo: 'Demo data', offline: 'Offline' })[this.agentState()],
  );

  ngOnInit(): void {
    this.api.getHealth().subscribe({
      next: (h) => this.health.set(h),
      error: () => this.failed.set(true),
    });
  }
}
