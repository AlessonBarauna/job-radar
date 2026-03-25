import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <div class="scanlines cyber-grid min-h-screen">
      <!-- Ambient glow decorativo no topo -->
      <div class="fixed top-0 left-1/2 -translate-x-1/2 w-[600px] h-[200px] pointer-events-none"
           style="background: radial-gradient(ellipse at center, rgba(0,212,255,0.06) 0%, transparent 70%); z-index: 0;">
      </div>
      <router-outlet />
    </div>
  `
})
export class AppComponent {}
