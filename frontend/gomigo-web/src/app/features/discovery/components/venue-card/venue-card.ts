import { Component, computed, input } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';

import { VenueCandidate } from '../../../../core/models/discovery.models';
import { CATEGORY_ICONS, VERIFICATION_LABELS } from '../../discovery-presets';

/** One ranked venue. Unknown values are shown as "Unknown", never guessed. */
@Component({
  selector: 'app-venue-card',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './venue-card.html',
})
export class VenueCard {
  readonly venue = input.required<VenueCandidate>();

  protected readonly icon = computed(() => CATEGORY_ICONS[this.venue().category] ?? 'place');
  protected readonly verificationLabel = computed(() => VERIFICATION_LABELS[this.venue().verificationStatus]);
  protected readonly where = computed(() => this.venue().address ?? this.venue().neighborhood);
}
