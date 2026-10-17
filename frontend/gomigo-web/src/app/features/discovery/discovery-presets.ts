import { ActivityCategory, ResearchRunStatus, VerificationStatus } from '../../core/models/discovery.models';

// Quick-pick choices from the Stitch design, mapped onto the API's numeric/enum contract.

export interface TimePreset {
  label: string;
  minutes: number;
}

export const TIME_PRESETS: TimePreset[] = [
  { label: '1 hour', minutes: 60 },
  { label: '2–3 hours', minutes: 180 },
  { label: 'Half a day', minutes: 360 },
  { label: 'Full day', minutes: 720 },
];

export interface BudgetPreset {
  label: string;
  /** Upper bound sent as budgetPerPerson (INR). */
  amount: number;
}

export const BUDGET_PRESETS: BudgetPreset[] = [
  { label: 'Under ₹300', amount: 300 },
  { label: '₹300–₹700', amount: 700 },
  { label: '₹700–₹1,500', amount: 1500 },
  { label: 'Flexible', amount: 10000 },
];

export interface MoodPreset {
  category: ActivityCategory;
  title: string;
  subtitle: string;
  icon: string;
}

export const MOOD_PRESETS: MoodPreset[] = [
  { category: 'Fun', title: 'Something fun', subtitle: 'Games, trivia, bowling', icon: 'sports_esports' },
  { category: 'Food', title: 'Food & coffee', subtitle: 'Specialty brew, street food', icon: 'local_cafe' },
  { category: 'Outdoors', title: 'Nature & relaxation', subtitle: 'Parks, gardens, quiet spots', icon: 'park' },
  { category: 'Culture', title: 'Events & culture', subtitle: 'Theatre, galleries, heritage', icon: 'confirmation_number' },
  { category: 'Any', title: 'Explore something new', subtitle: 'Surprise me with anything nearby', icon: 'explore' },
];

export interface CurationVisual {
  dot: 'secondary' | 'primary' | 'tertiary';
  imageUrl: string;
  imageAlt: string;
}

// Card text comes from GET /api/discovery/curations (area-aware); these are only the visuals per card key.
// Images are the Stitch mock-up assets; cards fall back to a plain tile if they fail to load.
export const CURATION_VISUALS: Record<string, CurationVisual> = {
  'hidden-gems': {
    dot: 'secondary',
    imageUrl:
      'https://lh3.googleusercontent.com/aida-public/AB6AXuBQv-u1S0kQ-qhQgI5g-zCdZOWStTEYtlDXTUzW7vHCgQr-bUaxZPwD7NcKYAwmc4un3ZT-ur-uTqMp1DpdORde18kQHjcDhT4LfpaMCKeHJFFPdiQjAQ3SRUkvQSCNje6Ms8BU71WLN-EQlIAd7EuT32hod5wCHYWgPqpHJOFi_JZZHC0tH4mGTNegVRZu7vfP9IC_vywXepztF5WrpoMZHLr2qTrW85JB3W1OurJPoHmIEDlHHykA',
    imageAlt: 'Sunlit bookstore courtyard with potted ferns',
  },
  food: {
    dot: 'primary',
    imageUrl:
      'https://lh3.googleusercontent.com/aida-public/AB6AXuA2LX06bIvqbilsyh7PbsHDZ2kzvrUaiqJzzG7l0SmnOThQHB6qZAaq-HLl5WMnDI51is8ZhQcQjC2WaOys44dUymhq79W11xdN4r17P08mt2cORgprrIFrfwg4P4vykAnluarMslhmnBOGSBTUvzy3S_AM6JDFOEsc4b8L9OLxlFPKZuEVLpvi0TV0k9BG_nTSZllkonT9cd0GaSG6fjywgfwU1-TUNQlKmvQU9DKrtYcc-WosAT4T',
    imageAlt: 'Busy coffee roastery with exposed brick',
  },
  outdoors: {
    dot: 'secondary',
    imageUrl:
      'https://lh3.googleusercontent.com/aida-public/AB6AXuD_QW4bR0DNXKAuUqBMHON7RzbrWxBDJfnI2rEVzImhv5ONWjLkCr9fyyESn_l3ZUqVwWzQZcxf6aIxYiZQOW3Et8BHjOMm4DP6AdaG_wIXp0iWxUO8w7mC5Zm56VE7BUxjFv294E3bQ0Jp4m7GSfcDUiRFFe4u4KP83VjxfJ6qVaSxzTKxuQnt5FsjAxvZVg24jB_DpWU7LUSzfFEdMA-jzVe1xNL-MMqxFBxCUitPvxBSYL6SstV_',
    imageAlt: 'Bamboo grove walkway in morning light',
  },
  activities: {
    dot: 'tertiary',
    imageUrl:
      'https://lh3.googleusercontent.com/aida-public/AB6AXuCgjyTu_lsLkiPwocam4I8Fg9FfoQPs69CbUf6oNCWxAiNtG20vPQChRYKL10l0dDU863Gu2aQzJiRTM-1UMx9rb6Il3s2lG3pay-9xrwH4TL7K7rbnCDkAbmHFR2t3h9L76QHq9OU-K3jF81iT8JLPZCSCoxdUtLElnrwRiloJhYQpvSdmexr7yZ8v9fDxq_h90pGwXJYPIbN_4wBuwLSrNZtALw8d04PWamQt4sPNTWmN6syfgrIk',
    imageAlt: 'Friends playing a board game in a café',
  },
};

export const CATEGORY_ICONS: Record<ActivityCategory, string> = {
  Any: 'explore',
  Fun: 'sports_esports',
  Food: 'local_cafe',
  Culture: 'theater_comedy',
  Outdoors: 'park',
  Nightlife: 'nightlife',
  Shopping: 'shopping_bag',
  Relaxation: 'spa',
  Other: 'place',
};

export const STATUS_LABELS: Record<ResearchRunStatus, string> = {
  Pending: 'Queued',
  Running: 'Researching',
  Completed: 'Ready',
  PartiallyCompleted: 'Partial results',
  Failed: 'Failed',
};

export const VERIFICATION_LABELS: Record<VerificationStatus, string> = {
  Verified: 'Source verified',
  PartiallyVerified: 'Partly verified',
  Unverified: 'Unverified',
};
