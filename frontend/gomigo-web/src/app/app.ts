import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { AppHeader } from './core/layout/app-header';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, AppHeader],
  templateUrl: './app.html',
})
export class App {
  protected readonly year = new Date().getFullYear();
}
