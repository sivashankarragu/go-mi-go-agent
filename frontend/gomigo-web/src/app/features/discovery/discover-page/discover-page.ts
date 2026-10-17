import { Component, DestroyRef, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { Subject, catchError, debounceTime, distinctUntilChanged, filter, map, of, startWith, switchMap, tap } from 'rxjs';

import { GoMigoApiService } from '../../../core/api/gomigo-api.service';
import {
  ActivityCategory,
  Curation,
  CurationsResponse,
  DiscoveryRequest,
  PlaceKind,
  PlaceSuggestion,
} from '../../../core/models/discovery.models';
import { AgentActivity } from '../components/agent-activity/agent-activity';
import { VenueCard } from '../components/venue-card/venue-card';
import { ResearchSession } from '../research-session.service';
import { BUDGET_PRESETS, MOOD_PRESETS, STATUS_LABELS, TIME_PRESETS } from '../discovery-presets';

/** Manual location is always enough; coordinates are optional. */
function locationOrCoordinates(group: AbstractControl): ValidationErrors | null {
  const { locationText, latitude, longitude } = group.value as {
    locationText: string;
    latitude: number | null;
    longitude: number | null;
  };
  return locationText?.trim() || (latitude != null && longitude != null) ? null : { locationRequired: true };
}

/** Examples cycled in the empty search box. */
const PLACEHOLDER_EXAMPLES = ['Indiranagar', 'Koramangala 5th Block', 'Toit, 100 Feet Road', 'Church Street', 'HSR Layout', 'Cubbon Park'];

const QUICK_IDEA_ICONS: Record<string, string> = {
  'hidden-gems': 'diamond',
  food: 'local_cafe',
  outdoors: 'park',
  activities: 'theater_comedy',
};

/**
 * Search-first discovery page: one search box on a dark stage; options animate in on focus;
 * once a search starts, the box docks to the top and results stream in below.
 */
@Component({
  selector: 'app-discover-page',
  imports: [ReactiveFormsModule, DatePipe, VenueCard, AgentActivity],
  providers: [ResearchSession],
  templateUrl: './discover-page.html',
})
export class DiscoverPage {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(GoMigoApiService);
  protected readonly session = inject(ResearchSession);

  protected readonly timePresets = TIME_PRESETS;
  protected readonly budgetPresets = BUDGET_PRESETS;
  protected readonly moods = MOOD_PRESETS;
  protected readonly statusLabels = STATUS_LABELS;

  private readonly searchBox = viewChild<ElementRef<HTMLElement>>('searchBox');
  private readonly locationInput = viewChild<ElementRef<HTMLInputElement>>('locationInput');

  // ---- Layout state
  /** Search box or its options have focus: show the options panel. */
  protected readonly expanded = signal(false);
  /** A search has started: the box docks to the top and results show below. */
  protected readonly docked = computed(() => this.session.phase() !== 'idle');
  protected readonly inputError = signal<string | null>(null);

  // ---- Typewriter placeholder
  protected readonly typedPlaceholder = signal('');

  // ---- Location
  protected readonly locating = signal(false);
  protected readonly locationMessage = signal<string | null>(null);
  /** Where the form's coordinates came from: the device (GPS) or a picked suggestion. */
  protected readonly coordinateSource = signal<'gps' | 'place' | null>(null);
  protected readonly usingCurrentLocation = computed(() => this.coordinateSource() === 'gps');

  // ---- Place type-ahead (Google Maps style)
  private readonly placeQuery$ = new Subject<string>();
  protected readonly suggestions = signal<PlaceSuggestion[]>([]);
  protected readonly suggestionsOpen = signal(false);
  protected readonly suggestionsLoading = signal(false);
  protected readonly activeSuggestion = signal(-1);

  protected readonly form = this.fb.group(
    {
      locationText: this.fb.nonNullable.control('', [Validators.maxLength(200)]),
      latitude: this.fb.control<number | null>(null),
      longitude: this.fb.control<number | null>(null),
      durationMinutes: this.fb.nonNullable.control(180, [Validators.required, Validators.min(30), Validators.max(720)]),
      budgetPerPerson: this.fb.nonNullable.control(700, [Validators.required, Validators.min(0), Validators.max(100000)]),
      category: this.fb.nonNullable.control<ActivityCategory>('Any'),
    },
    { validators: locationOrCoordinates },
  );

  private readonly formValue = toSignal(this.form.valueChanges.pipe(map(() => this.form.getRawValue())), {
    initialValue: this.form.getRawValue(),
  });
  protected readonly selectedMinutes = computed(() => this.formValue().durationMinutes);
  protected readonly selectedBudget = computed(() => this.formValue().budgetPerPerson);
  protected readonly selectedCategory = computed(() => this.formValue().category);
  protected readonly hasCoordinates = computed(() => this.formValue().latitude != null);
  protected readonly hasText = computed(() => this.formValue().locationText.trim().length > 0);

  protected readonly selectedTimeLabel = computed(
    () => TIME_PRESETS.find((t) => t.minutes === this.selectedMinutes())?.label ?? `${this.selectedMinutes()} min`,
  );
  protected readonly selectedBudgetLabel = computed(
    () => BUDGET_PRESETS.find((b) => b.amount === this.selectedBudget())?.label ?? `₹${this.selectedBudget()}`,
  );
  protected readonly selectedMood = computed(() => MOOD_PRESETS.find((m) => m.category === this.selectedCategory()) ?? MOOD_PRESETS[0]);

  /** Mood name for the searched run (used in "no exact results"). */
  protected readonly searchedMood = computed(() => {
    const category = this.session.run()?.request.category;
    return MOOD_PRESETS.find((m) => m.category === category)?.title ?? category ?? '';
  });

  /** Area-aware quick ideas from the curations endpoint; reloaded when the location changes. */
  protected readonly curations = signal<CurationsResponse | null>(null);

  constructor() {
    const destroyRef = inject(DestroyRef);

    this.form.valueChanges
      .pipe(
        startWith(null),
        map(() => {
          const v = this.form.getRawValue();
          return { location: v.locationText.trim(), latitude: v.latitude, longitude: v.longitude };
        }),
        debounceTime(400),
        distinctUntilChanged((a, b) => a.location === b.location && a.latitude === b.latitude && a.longitude === b.longitude),
        switchMap((q) => this.api.getCurations(q).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe((res) => {
        if (res) this.curations.set(res);
      });

    this.placeQuery$
      .pipe(
        map((q) => q.trim()),
        debounceTime(250),
        distinctUntilChanged(),
        tap((q) => {
          if (q.length < 2) {
            this.suggestions.set([]);
            this.suggestionsLoading.set(false);
          }
        }),
        filter((q) => q.length >= 2),
        tap(() => this.suggestionsLoading.set(true)),
        switchMap((q) => this.api.autocompletePlaces(q).pipe(catchError(() => of([] as PlaceSuggestion[])))),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe((list) => {
        this.suggestions.set(list);
        this.suggestionsLoading.set(false);
        this.activeSuggestion.set(list.length ? 0 : -1);
      });

    this.startTypewriter(destroyRef);
  }

  // ---- Typewriter placeholder: types an example, pauses, deletes, moves on. Static when motion is reduced.
  private startTypewriter(destroyRef: DestroyRef): void {
    if (typeof window === 'undefined' || window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) {
      this.typedPlaceholder.set(PLACEHOLDER_EXAMPLES[0]);
      return;
    }
    let example = 0;
    let chars = 0;
    let deleting = false;
    let pause = 0;
    const timer = setInterval(() => {
      if (pause > 0) {
        pause--;
        return;
      }
      const word = PLACEHOLDER_EXAMPLES[example];
      chars += deleting ? -1 : 1;
      this.typedPlaceholder.set(word.slice(0, chars));
      if (!deleting && chars === word.length) {
        deleting = true;
        pause = 22; // ~1.8 s
      } else if (deleting && chars === 0) {
        deleting = false;
        example = (example + 1) % PLACEHOLDER_EXAMPLES.length;
        pause = 4;
      }
    }, 80);
    destroyRef.onDestroy(() => clearInterval(timer));
  }

  // ---- Focus handling for the expandable search box
  protected onSearchFocusIn(): void {
    this.expanded.set(true);
  }

  protected onSearchFocusOut(event: FocusEvent): void {
    const next = event.relatedTarget as Node | null;
    if (next && this.searchBox()?.nativeElement.contains(next)) return;
    // Delay so a click on a suggestion or chip lands first.
    setTimeout(() => {
      if (!this.searchBox()?.nativeElement.contains(document.activeElement)) {
        this.expanded.set(false);
        this.closeSuggestions();
      }
    }, 150);
  }

  protected selectTime(minutes: number): void {
    this.form.controls.durationMinutes.setValue(minutes);
  }

  protected selectBudget(amount: number): void {
    this.form.controls.budgetPerPerson.setValue(amount);
  }

  protected selectMood(category: ActivityCategory): void {
    this.form.controls.category.setValue(category);
  }

  protected useMyLocation(): void {
    if (!('geolocation' in navigator)) {
      this.locationMessage.set('Location sharing is not available in this browser. Type a place instead.');
      return;
    }
    this.locating.set(true);
    this.locationMessage.set(null);
    this.inputError.set(null);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        const { latitude, longitude } = pos.coords;
        this.form.patchValue({ latitude, longitude });
        this.coordinateSource.set('gps');
        this.closeSuggestions();
        // Show where the user actually is, e.g. "19th Main Road, Sector 2, Bengaluru".
        this.api.reverseGeocode(latitude, longitude).subscribe({
          next: (place) => {
            this.locating.set(false);
            if (this.coordinateSource() !== 'gps') return; // user moved on meanwhile
            this.form.controls.locationText.setValue(place.label);
            this.locationMessage.set(
              place.nearestArea && !place.label.includes(place.nearestArea)
                ? `Using your current location · near ${place.nearestArea}`
                : 'Using your current location',
            );
          },
          error: () => {
            this.locating.set(false);
            this.locationMessage.set('Using your current location');
          },
        });
      },
      () => {
        this.locating.set(false);
        this.locationMessage.set('Could not get your location. Type a place instead.');
      },
      { timeout: 10000, maximumAge: 300000 },
    );
  }

  protected clearLocation(): void {
    this.form.patchValue({ locationText: '', latitude: null, longitude: null });
    this.coordinateSource.set(null);
    this.locationMessage.set(null);
    this.suggestions.set([]);
    this.locationInput()?.nativeElement.focus();
  }

  /** Typing a location overrides a previously shared position and opens the suggestion list. */
  protected onLocationTyped(text: string): void {
    if (this.hasCoordinates()) {
      this.form.patchValue({ latitude: null, longitude: null });
      this.coordinateSource.set(null);
      this.locationMessage.set(null);
    }
    this.inputError.set(null);
    this.suggestionsOpen.set(true);
    // Use the event's text: this handler can run before the form control has been updated.
    this.placeQuery$.next(text ?? '');
  }

  protected onLocationKeydown(event: KeyboardEvent): void {
    const list = this.suggestions();
    const listOpen = this.suggestionsOpen() && list.length > 0;
    switch (event.key) {
      case 'ArrowDown':
        if (!listOpen) return;
        event.preventDefault();
        this.activeSuggestion.update((i) => (i + 1) % list.length);
        break;
      case 'ArrowUp':
        if (!listOpen) return;
        event.preventDefault();
        this.activeSuggestion.update((i) => (i <= 0 ? list.length - 1 : i - 1));
        break;
      case 'Enter': {
        event.preventDefault();
        const picked = listOpen ? list[this.activeSuggestion()] : undefined;
        if (picked) this.selectSuggestion(picked);
        else this.submit();
        break;
      }
      case 'Escape':
        this.closeSuggestions();
        break;
    }
  }

  /** Picking a suggestion sets the place and its coordinates, then searches right away. */
  protected selectSuggestion(place: PlaceSuggestion): void {
    this.form.patchValue({ locationText: place.label, latitude: place.latitude, longitude: place.longitude });
    this.coordinateSource.set('place');
    this.locationMessage.set(null);
    this.closeSuggestions();
    this.submit();
  }

  protected closeSuggestions(): void {
    this.suggestionsOpen.set(false);
    this.activeSuggestion.set(-1);
  }

  protected placeIcon(kind: PlaceKind): string {
    return { Neighbourhood: 'location_city', Area: 'map', Street: 'add_road', Venue: 'location_on' }[kind] ?? 'location_on';
  }

  protected quickIdeaIcon(curation: Curation): string {
    return QUICK_IDEA_ICONS[curation.key] ?? 'explore';
  }

  /** Quick idea: set area and mood, then search. */
  protected useQuickIdea(curation: Curation): void {
    this.form.patchValue({ locationText: curation.locationText, category: curation.category });
    this.submit();
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.inputError.set('Type a place or use your current location');
      this.locationInput()?.nativeElement.focus();
      return;
    }
    const v = this.form.getRawValue();
    const request: DiscoveryRequest = {
      locationText: v.locationText.trim() || null,
      latitude: v.latitude,
      longitude: v.longitude,
      durationMinutes: v.durationMinutes,
      budgetPerPerson: v.budgetPerPerson,
      category: v.category,
      startTime: null,
      additionalPreferences: null,
    };
    this.expanded.set(false);
    this.closeSuggestions();
    this.locationInput()?.nativeElement.blur();
    this.session.start(request);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  protected newSearch(): void {
    this.session.reset();
    this.clearLocation();
  }

  protected fieldErrors(): [string, string[]][] {
    return Object.entries(this.session.error()?.fieldErrors ?? {});
  }
}
