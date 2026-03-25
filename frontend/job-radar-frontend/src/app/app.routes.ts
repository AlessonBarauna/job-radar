import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./components/search/search.component').then(m => m.SearchComponent)
  },
  {
    path: '**',
    redirectTo: ''
  }
];
