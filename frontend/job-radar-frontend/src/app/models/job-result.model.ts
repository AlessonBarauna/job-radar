export interface JobResult {
  id: number;
  title: string;
  snippet: string;
  author?: string;
  url: string;
  publishedAt: string;
  relevanceScore: number;
  matchedKeywords: string[];
  resultType: 'job' | 'post';
  relativeTime: string;
}

export interface SearchResponse {
  results: JobResult[];
  total: number;
  keywords: string[];
  provider: string;
  elapsedMs: number;
  searchedAt: string;
  fromCache: boolean;
}

export interface SearchHistory {
  id: number;
  keywords: string;
  resultCount: number;
  searchedAt: string;
  elapsedMs: number;
  provider: string;
  relativeTime: string;
}
