import { Component, Input, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JobResult, SearchResponse } from '../../models/job-result.model';
import { JobCardComponent } from '../job-card/job-card.component';

type TypeFilter   = 'all' | 'job' | 'post';
type SortOrder    = 'relevance' | 'date';
type SourceFilter = 'all' | string;

@Component({
  selector: 'app-results',
  standalone: true,
  imports: [CommonModule, FormsModule, JobCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (response) {

      <!-- ─── Meta da busca ─────────────────────────────────────────── -->
      <div class="flex flex-wrap items-center justify-between gap-3 mb-5 px-1">
        <div class="flex items-center gap-3">
          <span class="text-xl font-bold text-white">
            {{ filteredResults().length }}
            <span class="text-gray-500 text-base font-normal">
              de {{ response.total }} resultados
            </span>
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

      <!-- ─── Barra de filtros ──────────────────────────────────────── -->
      <div class="flex flex-wrap gap-2 mb-5 items-center justify-between">

        <!-- Filtro por tipo -->
        <div class="flex gap-2">
          @for (opt of typeOptions; track opt.value) {
            <button (click)="typeFilter.set(opt.value)"
                    class="text-xs px-3 py-1.5 rounded-lg font-mono transition-all duration-200"
                    [class]="typeFilter() === opt.value
                      ? 'bg-cyber-blue/20 text-cyber-blue border border-cyber-blue/40'
                      : 'text-gray-500 border border-cyber-border/60 hover:border-cyber-blue/30'">
              {{ opt.label }} ({{ countByType(opt.value) }})
            </button>
          }
        </div>

        <div class="flex gap-2 items-center">
          <!-- Filtro por provedor -->
          @if (availableSources().length > 1) {
            <select [(ngModel)]="sourceFilterValue"
                    class="text-xs bg-transparent border border-cyber-border/60 text-gray-400
                           rounded-lg px-2 py-1.5 font-mono cursor-pointer
                           hover:border-cyber-blue/30 focus:outline-none focus:border-cyber-blue/50">
              <option value="all">Todos os provedores</option>
              @for (src of availableSources(); track src) {
                <option [value]="src">{{ src }}</option>
              }
            </select>
          }

          <!-- Ordenação -->
          <select [(ngModel)]="sortOrderValue"
                  class="text-xs bg-transparent border border-cyber-border/60 text-gray-400
                         rounded-lg px-2 py-1.5 font-mono cursor-pointer
                         hover:border-cyber-blue/30 focus:outline-none focus:border-cyber-blue/50">
            <option value="relevance">Por relevância</option>
            <option value="date">Mais recentes</option>
          </select>
        </div>
      </div>

      <!-- ─── Grid de cards ─────────────────────────────────────────── -->
      @if (filteredResults().length > 0) {
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
          @for (result of filteredResults(); track result.url; let i = $index) {
            <div [style.animation-delay]="(i * 40) + 'ms'" class="animate-slide-up">
              <app-job-card [result]="result" />
            </div>
          }
        </div>
      } @else {
        <div class="text-center py-16 text-gray-600 font-mono">
          <p class="text-4xl mb-3">∅</p>
          <p>Nenhum resultado para os filtros selecionados.</p>
          <button (click)="resetFilters()"
                  class="mt-3 text-xs text-cyber-blue hover:underline">
            Limpar filtros
          </button>
        </div>
      }
    }
  `
})
export class ResultsComponent {
  @Input() set response(val: SearchResponse | null) {
    this._response = val;
    this.resetFilters();
  }
  get response() { return this._response; }
  private _response: SearchResponse | null = null;

  // ─── Signals de filtro ───────────────────────────────────────────────
  typeFilter   = signal<TypeFilter>('all');
  sortOrder    = signal<SortOrder>('relevance');
  sourceFilter = signal<SourceFilter>('all');

  // Wrappers para ngModel (signals não são suportados diretamente no ngModel)
  get sortOrderValue()    { return this.sortOrder(); }
  set sortOrderValue(v: SortOrder)   { this.sortOrder.set(v); }
  get sourceFilterValue() { return this.sourceFilter(); }
  set sourceFilterValue(v: string)   { this.sourceFilter.set(v); }

  readonly typeOptions = [
    { value: 'all'  as TypeFilter, label: 'Todos'  },
    { value: 'job'  as TypeFilter, label: 'Vagas'  },
    { value: 'post' as TypeFilter, label: 'Posts'  },
  ];

  // ─── Computed ───────────────────────────────────────────────────────
  availableSources = computed<string[]>(() => {
    if (!this._response) return [];
    return [...new Set(this._response.results.map(r => r.source).filter(Boolean))];
  });

  filteredResults = computed<JobResult[]>(() => {
    if (!this._response) return [];
    let results = [...this._response.results];

    if (this.typeFilter() !== 'all')
      results = results.filter(r => r.resultType === this.typeFilter());

    if (this.sourceFilter() !== 'all')
      results = results.filter(r => r.source === this.sourceFilter());

    if (this.sortOrder() === 'date')
      results.sort((a, b) => new Date(b.publishedAt).getTime() - new Date(a.publishedAt).getTime());

    return results;
  });

  // ─── Helpers ─────────────────────────────────────────────────────────
  countByType(type: TypeFilter): number {
    if (!this._response) return 0;
    if (type === 'all') return this._response.results.length;
    return this._response.results.filter(r => r.resultType === type).length;
  }

  resetFilters(): void {
    this.typeFilter.set('all');
    this.sortOrder.set('relevance');
    this.sourceFilter.set('all');
  }

  formatTime(dateStr: string): string {
    return new Date(dateStr).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
  }
}
