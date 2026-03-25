import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { JobResult, SearchResponse } from '../../models/job-result.model';
import { JobCardComponent } from '../job-card/job-card.component';

@Component({
  selector: 'app-results',
  standalone: true,
  imports: [CommonModule, JobCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (response) {
      <!-- Meta da busca -->
      <div class="flex flex-wrap items-center justify-between gap-3 mb-6 px-1">
        <div class="flex items-center gap-3">
          <span class="text-xl font-bold text-white">
            {{ response.total }}
            <span class="text-gray-500 text-base font-normal">resultados</span>
          </span>

          @if (response.fromCache) {
            <span class="text-xs font-mono px-2 py-0.5 rounded"
                  style="background:rgba(251,191,36,0.1);color:#fbbf24;border:1px solid rgba(251,191,36,0.3)">
              ⚡ cache
            </span>
          }
        </div>

        <div class="flex items-center gap-4 text-xs text-gray-500 font-mono">
          <span class="flex items-center gap-1">
            <span class="w-1.5 h-1.5 rounded-full bg-cyber-green animate-pulse-slow"></span>
            {{ response.provider }}
          </span>
          <span>{{ response.elapsedMs }}ms</span>
          <span>{{ formatTime(response.searchedAt) }}</span>
        </div>
      </div>

      <!-- Filtro inline por tipo -->
      <div class="flex gap-2 mb-5">
        <button (click)="filter = 'all'"
                class="text-xs px-3 py-1.5 rounded-lg font-mono transition-all duration-200"
                [class]="filter === 'all' ? 'bg-cyber-blue/20 text-cyber-blue border border-cyber-blue/40' : 'text-gray-500 border border-cyber-border hover:border-cyber-border/80'">
          Todos ({{ response.total }})
        </button>
        <button (click)="filter = 'job'"
                class="text-xs px-3 py-1.5 rounded-lg font-mono transition-all duration-200"
                [class]="filter === 'job' ? 'tag-job' : 'text-gray-500 border border-cyber-border hover:border-cyber-border/80'">
          Vagas ({{ countByType('job') }})
        </button>
        <button (click)="filter = 'post'"
                class="text-xs px-3 py-1.5 rounded-lg font-mono transition-all duration-200"
                [class]="filter === 'post' ? 'tag-post' : 'text-gray-500 border border-cyber-border hover:border-cyber-border/80'">
          Posts ({{ countByType('post') }})
        </button>
      </div>

      <!-- Grid de cards -->
      @if (filteredResults.length > 0) {
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
          @for (result of filteredResults; track result.id; let i = $index) {
            <div [style.animation-delay]="(i * 50) + 'ms'" class="animate-slide-up">
              <app-job-card [result]="result" />
            </div>
          }
        </div>
      } @else {
        <div class="text-center py-16 text-gray-600 font-mono">
          <p class="text-4xl mb-3">∅</p>
          <p>Nenhum resultado para este filtro.</p>
        </div>
      }
    }
  `
})
export class ResultsComponent {
  @Input() response: SearchResponse | null = null;

  filter: 'all' | 'job' | 'post' = 'all';

  get filteredResults(): JobResult[] {
    if (!this.response) return [];
    if (this.filter === 'all') return this.response.results;
    return this.response.results.filter(r => r.resultType === this.filter);
  }

  countByType(type: 'job' | 'post'): number {
    return this.response?.results.filter(r => r.resultType === type).length ?? 0;
  }

  formatTime(dateStr: string): string {
    return new Date(dateStr).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
  }
}
