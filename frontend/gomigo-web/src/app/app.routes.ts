import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'GoMigo · Discover',
    loadComponent: () =>
      import('./features/discovery/discover-page/discover-page').then((m) => m.DiscoverPage),
  },
  { path: '**', redirectTo: '' },
];
