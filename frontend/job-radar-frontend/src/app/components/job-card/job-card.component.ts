import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { JobResult } from '../../models/job-result.model';

@Component({
  selector: 'app-job-card',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article class="cyber-card animate-slide-up group relative overflow-hidden">
      <!-- Linha decorativa glow no topo do card -->
      <div class="absolute top-0 left-0 right-0 h-[1px] opacity-0 group-hover:opacity-100 transition-opacity duration-500"
           style="background: linear-gradient(90deg, transparent, #00d4ff, transparent);">
      </div>

      <!-- Header: tipo + provedor + tempo -->
      <div class="flex items-center justify-between mb-3">
        <div class="flex items-center gap-2">
          <span [class]="result.resultType === 'job' ? 'tag-job' : 'tag-post'">
            {{ result.resultType === 'job' ? '⚡ VAGA' : '📢 POST' }}
          </span>
          <!-- Badge de provedor -->
          <span class="text-xs font-mono px-1.5 py-0.5 rounded"
                [style]="getSourceStyle(result.source)">
            {{ result.source }}
          </span>
          <span class="text-xs text-gray-500 font-mono">{{ result.relativeTime }}</span>
        </div>

        <!-- Score de relevância -->
        <div class="flex items-center gap-2">
          <div class="flex flex-col items-end gap-1">
            <span class="text-xs text-gray-500 font-mono">relevância</span>
            <div class="flex items-center gap-2">
              <div class="w-16 h-1 bg-cyber-border rounded-full overflow-hidden">
                <div class="score-bar h-full transition-all duration-700"
                     [style.width.%]="result.relevanceScore">
                </div>
              </div>
              <span class="text-xs font-mono font-bold"
                    [class]="getScoreColor(result.relevanceScore)">
                {{ result.relevanceScore }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Título com keywords destacadas -->
      <h3 class="text-white font-semibold text-base mb-2 leading-snug group-hover:text-cyber-blue transition-colors duration-200"
          [innerHTML]="highlightKeywords(result.title, result.matchedKeywords)">
      </h3>

      <!-- Autor/empresa -->
      @if (result.author) {
        <div class="flex items-center gap-2 mb-3">
          <div class="w-5 h-5 rounded-full flex items-center justify-center text-xs"
               style="background: linear-gradient(135deg, #00d4ff, #8b5cf6);">
            {{ result.author.charAt(0).toUpperCase() }}
          </div>
          <span class="text-sm text-cyber-blue/80 font-medium">{{ result.author }}</span>
        </div>
      }

      <!-- Snippet com keywords destacadas -->
      <p class="text-gray-400 text-sm leading-relaxed line-clamp-3 mb-4"
         [innerHTML]="highlightKeywords(result.snippet, result.matchedKeywords)">
      </p>

      <!-- Keywords matched -->
      @if (result.matchedKeywords.length > 0) {
        <div class="flex flex-wrap gap-1.5 mb-4">
          @for (kw of result.matchedKeywords; track kw) {
            <span class="keyword-badge">{{ kw }}</span>
          }
        </div>
      }

      <!-- Footer: data + botão -->
      <div class="flex items-center justify-between pt-3 border-t border-cyber-border/50">
        <span class="text-xs text-gray-600 font-mono">
          {{ formatDate(result.publishedAt) }}
        </span>

        <a [href]="result.url"
           target="_blank"
           rel="noopener noreferrer"
           class="flex items-center gap-2 text-xs font-semibold px-4 py-2 rounded-lg transition-all duration-200"
           style="background: rgba(0, 212, 255, 0.1); color: #00d4ff; border: 1px solid rgba(0, 212, 255, 0.3);"
           onmouseover="this.style.background='rgba(0,212,255,0.2)'; this.style.boxShadow='0 0 12px rgba(0,212,255,0.3)'"
           onmouseout="this.style.background='rgba(0,212,255,0.1)'; this.style.boxShadow='none'">
          <!-- Ícone LinkedIn apenas para vagas do LinkedIn -->
          @if (result.source === 'LinkedIn') {
            <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
              <path d="M19 0h-14c-2.761 0-5 2.239-5 5v14c0 2.761 2.239 5 5 5h14c2.762 0 5-2.239 5-5v-14c0-2.761-2.238-5-5-5zm-11 19h-3v-11h3v11zm-1.5-12.268c-.966 0-1.75-.79-1.75-1.764s.784-1.764 1.75-1.764 1.75.79 1.75 1.764-.783 1.764-1.75 1.764zm13.5 12.268h-3v-5.604c0-3.368-4-3.113-4 0v5.604h-3v-11h3v1.765c1.396-2.586 7-2.777 7 2.476v6.759z"/>
            </svg>
          } @else {
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M18 13v6a2 2 0 01-2 2H5a2 2 0 01-2-2V8a2 2 0 012-2h6M15 3h6v6M10 14L21 3"/>
            </svg>
          }
          Ver vaga
        </a>
      </div>
    </article>
  `
})
export class JobCardComponent {
  @Input({ required: true }) result!: JobResult;

  /** Destaca palavras-chave com span colorido */
  highlightKeywords(text: string, keywords: string[]): string {
    if (!keywords.length) return this.escapeHtml(text);
    let result = this.escapeHtml(text);
    for (const kw of keywords) {
      const escaped = kw.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      const regex = new RegExp(`(${escaped})`, 'gi');
      result = result.replace(regex,
        '<mark style="background:rgba(0,212,255,0.15);color:#00d4ff;border-radius:3px;padding:0 2px">$1</mark>'
      );
    }
    return result;
  }

  getSourceStyle(source: string): string {
    const styles: Record<string, string> = {
      'Remotive':  'background:rgba(0,212,255,0.1);color:#00d4ff;border:1px solid rgba(0,212,255,0.25)',
      'Indeed':    'background:rgba(139,92,246,0.1);color:#a78bfa;border:1px solid rgba(139,92,246,0.25)',
      'LinkedIn':  'background:rgba(14,118,168,0.15);color:#60b8f5;border:1px solid rgba(14,118,168,0.3)',
      'Jooble':    'background:rgba(0,255,136,0.08);color:#00ff88;border:1px solid rgba(0,255,136,0.2)',
      'Glassdoor': 'background:rgba(0,255,136,0.08);color:#00ff88;border:1px solid rgba(0,255,136,0.2)',
    };
    return styles[source] ?? 'background:rgba(100,100,100,0.1);color:#9ca3af;border:1px solid rgba(100,100,100,0.2)';
  }

  getScoreColor(score: number): string {
    if (score >= 75) return 'text-cyber-green';
    if (score >= 50) return 'text-cyber-blue';
    if (score >= 25) return 'text-cyber-yellow';
    return 'text-gray-500';
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString('pt-BR', {
      day: '2-digit', month: '2-digit',
      hour: '2-digit', minute: '2-digit'
    });
  }

  private escapeHtml(text: string): string {
    return text
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }
}
