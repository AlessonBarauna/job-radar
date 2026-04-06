import { Component, Input, ChangeDetectionStrategy, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReportResponse } from '../../models/job-result.model';
import { marked } from 'marked';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Component({
  selector: 'app-report',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (report) {
      <div class="cyber-card">

        <!-- Header do relatório -->
        <div class="flex items-center justify-between mb-6 pb-4 border-b border-cyber-border/40">
          <div class="flex items-center gap-3">
            <div class="w-8 h-8 rounded-lg flex items-center justify-center text-lg"
                 style="background:linear-gradient(135deg,rgba(139,92,246,0.2),rgba(0,212,255,0.2));border:1px solid rgba(139,92,246,0.3)">
              🤖
            </div>
            <div>
              <h2 class="text-white font-bold text-sm">Análise de Mercado</h2>
              <p class="text-xs text-gray-500 font-mono">{{ report.keywords }}</p>
            </div>
          </div>
          <div class="flex items-center gap-3 text-xs font-mono text-gray-600">
            @if (report.fromCache) {
              <span style="color:#fbbf24">⚡ cache</span>
            }
            <span>{{ report.elapsedMs }}ms</span>
            <span>{{ formatDate(report.generatedAt) }}</span>
          </div>
        </div>

        <!-- Conteúdo markdown renderizado -->
        <div class="report-content prose-cyber" [innerHTML]="renderedMarkdown"></div>

      </div>
    }
  `,
  styles: [`
    :host ::ng-deep .report-content {
      color: #d1d5db;
      line-height: 1.7;
    }
    :host ::ng-deep .report-content h1 {
      font-size: 1.4rem;
      font-weight: 700;
      color: white;
      margin: 1.5rem 0 0.75rem;
      padding-bottom: 0.4rem;
      border-bottom: 1px solid rgba(0,212,255,0.2);
    }
    :host ::ng-deep .report-content h2 {
      font-size: 1.1rem;
      font-weight: 600;
      color: #00d4ff;
      margin: 1.4rem 0 0.6rem;
    }
    :host ::ng-deep .report-content h3 {
      font-size: 0.95rem;
      font-weight: 600;
      color: #a78bfa;
      margin: 1.1rem 0 0.4rem;
    }
    :host ::ng-deep .report-content p {
      margin: 0.5rem 0;
      color: #9ca3af;
    }
    :host ::ng-deep .report-content ul, :host ::ng-deep .report-content ol {
      margin: 0.4rem 0 0.4rem 1.2rem;
    }
    :host ::ng-deep .report-content li {
      margin: 0.2rem 0;
      color: #9ca3af;
    }
    :host ::ng-deep .report-content strong {
      color: #e5e7eb;
      font-weight: 600;
    }
    :host ::ng-deep .report-content code {
      background: rgba(0,212,255,0.08);
      color: #00d4ff;
      padding: 0.1rem 0.3rem;
      border-radius: 3px;
      font-size: 0.85em;
      font-family: monospace;
    }
    :host ::ng-deep .report-content pre {
      background: rgba(0,0,0,0.4);
      border: 1px solid rgba(0,212,255,0.15);
      border-radius: 8px;
      padding: 1rem;
      overflow-x: auto;
      margin: 0.8rem 0;
    }
    :host ::ng-deep .report-content pre code {
      background: none;
      color: #e5e7eb;
      padding: 0;
    }
    :host ::ng-deep .report-content table {
      width: 100%;
      border-collapse: collapse;
      margin: 0.8rem 0;
      font-size: 0.85rem;
    }
    :host ::ng-deep .report-content th {
      background: rgba(0,212,255,0.08);
      color: #00d4ff;
      padding: 0.5rem 0.75rem;
      text-align: left;
      border: 1px solid rgba(0,212,255,0.15);
      font-size: 0.8rem;
      font-family: monospace;
    }
    :host ::ng-deep .report-content td {
      padding: 0.4rem 0.75rem;
      border: 1px solid rgba(255,255,255,0.06);
      color: #9ca3af;
    }
    :host ::ng-deep .report-content tr:hover td {
      background: rgba(255,255,255,0.02);
    }
    :host ::ng-deep .report-content a {
      color: #60b8f5;
      text-decoration: none;
    }
    :host ::ng-deep .report-content a:hover {
      text-decoration: underline;
    }
    :host ::ng-deep .report-content blockquote {
      border-left: 3px solid rgba(139,92,246,0.5);
      padding-left: 1rem;
      color: #6b7280;
      margin: 0.8rem 0;
      font-style: italic;
    }
    :host ::ng-deep .report-content hr {
      border: none;
      border-top: 1px solid rgba(255,255,255,0.08);
      margin: 1.5rem 0;
    }
  `]
})
export class ReportComponent implements OnChanges {
  @Input() report: ReportResponse | null = null;

  renderedMarkdown: SafeHtml = '';

  constructor(private sanitizer: DomSanitizer) {}

  ngOnChanges(): void {
    if (this.report?.markdown) {
      const html = marked.parse(this.report.markdown) as string;
      this.renderedMarkdown = this.sanitizer.bypassSecurityTrustHtml(html);
    }
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString('pt-BR', {
      day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit'
    });
  }
}
