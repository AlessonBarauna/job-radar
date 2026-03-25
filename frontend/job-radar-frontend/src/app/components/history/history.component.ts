import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SearchHistory } from '../../models/job-result.model';

@Component({
  selector: 'app-history',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="cyber-card">
      <h3 class="text-xs text-gray-500 uppercase tracking-widest font-mono mb-4 flex items-center gap-2">
        <span class="inline-block w-2 h-2 rounded-full bg-cyber-purple animate-pulse-slow"></span>
        Histórico
      </h3>

      @if (!history.length) {
        <p class="text-gray-600 text-xs font-mono text-center py-4">sem buscas anteriores</p>
      } @else {
        <ul class="space-y-2">
          @for (item of history; track item.id) {
            <li>
              <button
                (click)="onSelect.emit(item.keywords)"
                class="w-full text-left group flex items-center justify-between px-3 py-2 rounded-lg
                       transition-all duration-200 hover:bg-cyber-border/30"
                [title]="item.keywords">
                <!-- Keywords -->
                <div class="flex-1 min-w-0">
                  <div class="text-sm text-gray-300 group-hover:text-cyber-blue transition-colors truncate font-mono">
                    {{ item.keywords }}
                  </div>
                  <div class="text-xs text-gray-600 mt-0.5">
                    {{ item.resultCount }} resultados · {{ item.relativeTime }}
                  </div>
                </div>

                <!-- Seta -->
                <svg class="w-3 h-3 text-gray-600 group-hover:text-cyber-blue transition-colors ml-2 flex-shrink-0"
                     fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7"/>
                </svg>
              </button>
            </li>
          }
        </ul>
      }
    </aside>
  `
})
export class HistoryComponent {
  @Input() history: SearchHistory[] = [];
  @Output() onSelect = new EventEmitter<string>();
}
