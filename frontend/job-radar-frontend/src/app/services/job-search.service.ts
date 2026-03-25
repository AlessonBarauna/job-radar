import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { SearchResponse, SearchHistory } from '../models/job-result.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class JobSearchService {
  private http = inject(HttpClient);
  private apiUrl = environment.apiUrl;

  /**
   * Busca vagas/posts do LinkedIn pelas keywords fornecidas.
   * Retorna resultados das últimas 24h, ordenados por relevância.
   */
  search(keywords: string): Observable<SearchResponse> {
    const params = new HttpParams().set('keywords', keywords);
    return this.http.get<SearchResponse>(`${this.apiUrl}/api/jobs/search`, { params });
  }

  /**
   * Retorna histórico das buscas mais recentes.
   */
  getHistory(limit = 10): Observable<SearchHistory[]> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.http.get<SearchHistory[]>(`${this.apiUrl}/api/history`, { params });
  }
}
