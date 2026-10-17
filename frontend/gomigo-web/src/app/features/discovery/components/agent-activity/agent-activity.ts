import { Component, computed, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';

import { AgentSession } from '../../../../core/models/discovery.models';
import { environment } from '../../../../../environments/environment';

/** Only TinyFish's own stream hosts may be embedded. */
const LIVE_VIEW_HOST_SUFFIXES = ['.tinyfish.io', '.tinyfish.ai'];

/**
 * Shows what a research agent is doing: the page it opened, a live view of its browser while it runs,
 * and each step with a screenshot. Turns the research from a black box into something you can watch.
 */
@Component({
  selector: 'app-agent-activity',
  imports: [DatePipe],
  templateUrl: './agent-activity.html',
})
export class AgentActivity {
  private readonly sanitizer = inject(DomSanitizer);

  readonly agent = input.required<AgentSession>();

  /** Some steps list a screenshot the provider never produces (often the final step); those are hidden. */
  protected readonly brokenShots = signal<ReadonlySet<string>>(new Set());

  protected readonly startHost = computed(() => {
    try {
      const u = new URL(this.agent().startUrl);
      return u.host + (u.pathname.length > 1 ? u.pathname : '');
    } catch {
      return this.agent().startUrl;
    }
  });

  /** Trusted only for https URLs on TinyFish hosts; anything else is not embedded. */
  protected readonly liveView = computed<SafeResourceUrl | null>(() => {
    const url = this.agent().isActive ? this.agent().liveViewUrl : null;
    if (!url) return null;
    try {
      const u = new URL(url);
      const allowed = u.protocol === 'https:' && LIVE_VIEW_HOST_SUFFIXES.some((s) => u.hostname.endsWith(s));
      return allowed ? this.sanitizer.bypassSecurityTrustResourceUrl(u.href) : null;
    } catch {
      return null;
    }
  });

  protected readonly latestScreenshot = computed(() => {
    const steps = this.agent().steps;
    for (let i = steps.length - 1; i >= 0; i--) {
      const url = steps[i].screenshotUrl;
      if (url && !this.brokenShots().has(url)) return this.screenshotSrc(url);
    }
    return null;
  });

  protected markBroken(path: string): void {
    this.brokenShots.update((s) => new Set([...s, path]));
  }

  /** Path of the screenshot behind a full src URL (for the error handler of the large image). */
  protected markBrokenSrc(src: string): void {
    const step = this.agent().steps.find((s) => s.screenshotUrl && this.screenshotSrc(s.screenshotUrl) === src);
    if (step?.screenshotUrl) this.markBroken(step.screenshotUrl);
  }

  protected screenshotSrc(path: string): string {
    return `${environment.apiBaseUrl}${path}`;
  }
}
