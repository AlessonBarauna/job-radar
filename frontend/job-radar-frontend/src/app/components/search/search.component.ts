import {
  Component, OnInit, signal, computed, inject, ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { JobSearchService } from '../../services/job-search.service';
import { SearchResponse, SearchHistory, ReportResponse } from '../../models/job-result.model';
import { ResultsComponent } from '../results/results.component';
import { HistoryComponent } from '../history/history.component';
import { ReportComponent } from '../report/report.component';

@Component({
  selector: 'app-search',
  standalone: true,
  imports: [CommonModule, FormsModule, ResultsComponent, HistoryComponent, ReportComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen flex flex-col">

      <!-- ═══ HEADER ═══════════════════════════════════════════ -->
      <header class="relative pt-16 pb-10 px-4 text-center overflow-hidden">
        <!-- Círculo glow atrás do título -->
        <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-96 rounded-full pointer-events-none"
             style="background: radial-gradient(circle, rgba(139,92,246,0.08) 0%, transparent 70%)">
        </div>

        <!-- Logo / título -->
        <div class="relative z-10 mb-2 inline-flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl flex items-center justify-center text-xl"
               style="background: linear-gradient(135deg, #00d4ff20, #8b5cf620); border: 1px solid rgba(0,212,255,0.3)">
            🎯
          </div>
          <h1 class="text-3xl font-bold tracking-tight">
            <span style="background: linear-gradient(90deg, #00d4ff, #8b5cf6); -webkit-background-clip: text; -webkit-text-fill-color: transparent;">
              Job
            </span>
            <span class="text-white">Radar</span>
          </h1>
        </div>

        <p class="text-gray-500 text-sm font-mono mt-2 relative z-10">
          Gupy BR · Jobicy · Remotive // vagas reais // ordenadas por relevância
        </p>

        <!-- Linha decorativa -->
        <div class="mt-6 mx-auto max-w-md h-px"
             style="background: linear-gradient(90deg, transparent, rgba(0,212,255,0.4), rgba(139,92,246,0.4), transparent)">
        </div>
      </header>

      <!-- ═══ MAIN ═══════════════════════════════════════════ -->
      <main class="flex-1 w-full max-w-7xl mx-auto px-4 pb-16">

        <!-- ─── Search bar ─────────────────────────────── -->
        <section class="mb-10">
          <form (ngSubmit)="onSearch()" class="flex flex-col sm:flex-row gap-3 max-w-2xl mx-auto">
            <div class="relative flex-1">
              <!-- Ícone busca -->
              <svg class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500 pointer-events-none"
                   fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
              </svg>
              <input
                [(ngModel)]="searchInput"
                name="keywords"
                class="cyber-input pl-10"
                placeholder="dotnet, aws, angular, python, react..."
                autocomplete="off"
                [disabled]="loading() || loadingReport()"
                (keydown.enter)="onSearch()">
            </div>

            <button type="submit"
                    class="cyber-btn whitespace-nowrap"
                    [disabled]="loading() || loadingReport() || !searchInput.trim()">
              @if (loading()) {
                <svg class="animate-spin w-4 h-4" viewBox="0 0 24 24" fill="none">
                  <circle class="opacity-25" cx="12" cy="12" r="10"
                          stroke="currentColor" stroke-width="4"/>
                  <path class="opacity-75" fill="currentColor"
                        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
                </svg>
                Buscando...
              } @else {
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                </svg>
                Buscar
              }
            </button>

            <!-- Botão Relatório IA -->
            <button type="button"
                    (click)="onGenerateReport()"
                    class="whitespace-nowrap text-sm font-semibold px-4 py-2 rounded-lg transition-all duration-200 flex items-center gap-2"
                    [disabled]="loading() || loadingReport() || !searchInput.trim()"
                    style="background:rgba(139,92,246,0.15);color:#a78bfa;border:1px solid rgba(139,92,246,0.35)"
                    [style.opacity]="loading() || loadingReport() || !searchInput.trim() ? '0.4' : '1'">
              @if (loadingReport()) {
                <svg class="animate-spin w-4 h-4" viewBox="0 0 24 24" fill="none">
                  <circle class="opacity-25" cx="12" cy="12" r="10"
                          stroke="currentColor" stroke-width="4"/>
                  <path class="opacity-75" fill="currentColor"
                        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
                </svg>
                Gerando...
              } @else {
                🤖 Relatório IA
              }
            </button>
          </form>

          <!-- Sugestões rápidas -->
          <div class="flex flex-wrap gap-2 justify-center mt-4">
            @for (s of suggestions; track s) {
              <button (click)="quickSearch(s)"
                      class="text-xs px-3 py-1 rounded-full font-mono transition-all duration-200
                             text-gray-500 border border-cyber-border/50
                             hover:text-cyber-blue hover:border-cyber-blue/40">
                {{ s }}
              </button>
            }
          </div>
        </section>

        <!-- ─── Error ──────────────────────────────────── -->
        @if (error()) {
          <div class="max-w-2xl mx-auto mb-8 p-4 rounded-xl border flex items-start gap-3 animate-fade-in"
               style="background:rgba(239,68,68,0.05); border-color:rgba(239,68,68,0.3)">
            <span class="text-red-400 text-lg mt-0.5">⚠</span>
            <div>
              <p class="text-red-400 text-sm font-medium">Erro ao buscar vagas</p>
              <p class="text-red-400/70 text-xs mt-1 font-mono">{{ error() }}</p>
            </div>
          </div>
        }

        <!-- ─── Toggle vagas / relatório ─────────────── -->
        @if (searchResponse() || reportResponse()) {
          <div class="flex gap-2 mb-6 max-w-xs">
            <button (click)="activeView.set('results')"
                    class="flex-1 text-xs px-3 py-1.5 rounded-lg font-mono transition-all duration-200"
                    [class]="activeView() === 'results'
                      ? 'bg-cyber-blue/20 text-cyber-blue border border-cyber-blue/40'
                      : 'text-gray-500 border border-cyber-border/60 hover:border-cyber-blue/30'">
              ⚡ Vagas
            </button>
            <button (click)="activeView.set('report')"
                    class="flex-1 text-xs px-3 py-1.5 rounded-lg font-mono transition-all duration-200"
                    [class]="activeView() === 'report'
                      ? 'bg-cyber-purple/20 text-cyber-purple border border-cyber-purple/40'
                      : 'text-gray-500 border border-cyber-border/60 hover:border-cyber-purple/30'">
              🤖 Relatório IA
            </button>
          </div>
        }

        <!-- ─── Content: results + history ────────────── -->
        <div class="flex flex-col lg:flex-row gap-6">

          <!-- Results (coluna principal) -->
          <div class="flex-1 min-w-0">

            <!-- View: Relatório IA -->
            @if (activeView() === 'report') {
              @if (loadingReport()) {
                <div class="cyber-card text-center py-16">
                  <div class="text-4xl mb-4 animate-pulse">🤖</div>
                  <p class="text-gray-400 font-mono text-sm">Analisando mercado e gerando relatório...</p>
                  <p class="text-gray-600 font-mono text-xs mt-2">Isso pode levar alguns segundos</p>
                </div>
              } @else if (reportResponse()) {
                <app-report [report]="reportResponse()" />
              }
            }

            <!-- View: Vagas -->
            @if (activeView() === 'results') {
            @if (searchResponse()) {
              <app-results [response]="searchResponse()" />
            } @else if (!loading() && hasSearched()) {
              <div class="text-center py-20 text-gray-600">
                <p class="text-5xl mb-4 animate-float">📡</p>
                <p class="font-mono">Nenhum resultado encontrado para as últimas 24h.</p>
                <p class="text-sm mt-2">Tente outras palavras-chave.</p>
              </div>
            } @else if (!loading() && !hasSearched()) {
              <!-- Estado inicial -->
              <div class="text-center py-20">
                <div class="inline-flex flex-col items-center gap-4">
                  <div class="w-20 h-20 rounded-2xl flex items-center justify-center text-4xl animate-float"
                       style="background: linear-gradient(135deg, rgba(0,212,255,0.1), rgba(139,92,246,0.1)); border: 1px solid rgba(0,212,255,0.2)">
                    🎯
                  </div>
                  <p class="text-gray-500 font-mono text-sm">
                    Digite palavras-chave e pressione buscar
                  </p>
                  <div class="flex items-center gap-2 text-xs text-gray-600 font-mono">
                    <span class="text-cyber-green">✓</span> dados públicos reais
                    <span class="text-cyber-green ml-2">✓</span> Gupy · Jobicy · Remotive
                  </div>
                </div>
              </div>
            }
            } <!-- fecha @if activeView === results -->

            <!-- Loading skeleton -->
            @if (loading()) {
              <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
                @for (i of skeletons; track i) {
                  <div class="cyber-card animate-pulse">
                    <div class="flex justify-between mb-3">
                      <div class="h-4 w-16 rounded bg-cyber-border/50"></div>
                      <div class="h-4 w-20 rounded bg-cyber-border/50"></div>
                    </div>
                    <div class="h-5 w-3/4 rounded bg-cyber-border/50 mb-2"></div>
                    <div class="h-4 w-1/3 rounded bg-cyber-border/30 mb-3"></div>
                    <div class="space-y-2 mb-4">
                      <div class="h-3 w-full rounded bg-cyber-border/30"></div>
                      <div class="h-3 w-5/6 rounded bg-cyber-border/30"></div>
                      <div class="h-3 w-4/6 rounded bg-cyber-border/20"></div>
                    </div>
                    <div class="flex gap-2">
                      <div class="h-5 w-14 rounded bg-cyber-border/30"></div>
                      <div class="h-5 w-14 rounded bg-cyber-border/20"></div>
                    </div>
                  </div>
                }
              </div>
            }
          </div>

          <!-- Sidebar histórico -->
          <aside class="w-full lg:w-64 flex-shrink-0">
            <app-history
              [history]="history()"
              (onSelect)="quickSearch($event)" />

            <!-- Stats box -->
            @if (searchResponse()) {
              <div class="cyber-card mt-4">
                <h3 class="text-xs text-gray-500 uppercase tracking-widest font-mono mb-3 flex items-center gap-2">
                  <span class="inline-block w-2 h-2 rounded-full bg-cyber-blue animate-pulse-slow"></span>
                  Resumo
                </h3>
                <div class="space-y-2">
                  <div class="flex justify-between text-xs font-mono">
                    <span class="text-gray-500">vagas</span>
                    <span class="text-cyber-green">{{ countByType('job') }}</span>
                  </div>
                  <div class="flex justify-between text-xs font-mono">
                    <span class="text-gray-500">posts</span>
                    <span class="text-cyber-purple">{{ countByType('post') }}</span>
                  </div>
                  <div class="flex justify-between text-xs font-mono">
                    <span class="text-gray-500">provedor</span>
                    <span class="text-cyber-blue">{{ searchResponse()?.provider }}</span>
                  </div>
                  <div class="flex justify-between text-xs font-mono">
                    <span class="text-gray-500">tempo</span>
                    <span class="text-white">{{ searchResponse()?.elapsedMs }}ms</span>
                  </div>
                </div>
              </div>
            }
          </aside>
        </div>

      </main>

      <!-- ═══ FOOTER ═══════════════════════════════════════════ -->
      <footer class="border-t border-cyber-border/30 py-6 px-4 text-center">
        <p class="text-gray-600 text-xs font-mono">
          JobRadar v2.0 · Gupy BR · Jobicy · Remotive · dados públicos reais
        </p>
      </footer>
    </div>
  `
})
export class SearchComponent implements OnInit {
  private jobSearchService = inject(JobSearchService);

  // ─── State (signals) ─────────────────────────────────────────
  searchInput = '';
  loading        = signal(false);
  loadingReport  = signal(false);
  error          = signal<string | null>(null);
  searchResponse = signal<SearchResponse | null>(null);
  reportResponse = signal<ReportResponse | null>(null);
  history        = signal<SearchHistory[]>([]);
  hasSearched    = signal(false);
  activeView     = signal<'results' | 'report'>('results');

  skeletons = Array(6).fill(0);

  suggestions = [
    'dotnet, csharp', 'angular, typescript', 'aws, devops',
    'fullstack, react', 'python, django', 'node, backend',
    'java, spring', 'golang, backend'
  ];

  ngOnInit(): void {
    this.loadHistory();
  }

  onSearch(): void {
    const kw = this.searchInput.trim();
    if (!kw || this.loading()) return;
    this.executeSearch(kw);
  }

  quickSearch(keywords: string): void {
    this.searchInput = keywords;
    this.executeSearch(keywords);
  }

  onGenerateReport(): void {
    const kw = this.searchInput.trim();
    if (!kw || this.loadingReport()) return;

    this.loadingReport.set(true);
    this.error.set(null);
    this.activeView.set('report');

    this.jobSearchService.generateReport(kw).subscribe({
      next: (res) => {
        this.reportResponse.set(res);
        this.loadingReport.set(false);
      },
      error: (err) => {
        this.error.set(err?.error?.detail ?? 'Erro ao gerar relatório. Verifique se a chave OpenAI está configurada.');
        this.loadingReport.set(false);
        this.activeView.set('results');
      }
    });
  }

  private executeSearch(keywords: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.hasSearched.set(true);

    this.jobSearchService.search(keywords).subscribe({
      next: (res) => {
        this.searchResponse.set(res);
        this.loading.set(false);
        this.loadHistory();
      },
      error: (err) => {
        this.error.set(err?.error?.error ?? 'Erro de conexão com a API. Verifique se o backend está rodando.');
        this.loading.set(false);
        this.searchResponse.set(null);
      }
    });
  }

  private loadHistory(): void {
    this.jobSearchService.getHistory(10).subscribe({
      next: (h) => this.history.set(h),
      error: () => {} // silencia erro de histórico
    });
  }

  countByType(type: 'job' | 'post'): number {
    return this.searchResponse()?.results.filter(r => r.resultType === type).length ?? 0;
  }
}
