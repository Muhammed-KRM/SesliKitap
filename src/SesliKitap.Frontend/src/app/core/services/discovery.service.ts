import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BookDetail } from '../models/book.model';
import {
  BookImportRequest,
  DiscoveryFileDto,
  DiscoveryProviderDto,
  DiscoveryResultDto
} from '../models/discovery.model';

@Injectable({ providedIn: 'root' })
export class DiscoveryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/discovery`;

  getProviders(): Observable<DiscoveryProviderDto[]> {
    return this.http.get<DiscoveryProviderDto[]>(`${this.baseUrl}/providers`);
  }

  search(
    query: string,
    providerCodes: string[],
    page = 1
  ): Observable<DiscoveryResultDto[]> {
    let params = new HttpParams().set('q', query.trim()).set('sayfa', String(page));
    if (providerCodes.length > 0) {
      params = params.set('kaynaklar', providerCodes.join(','));
    }
    return this.http.get<DiscoveryResultDto[]>(`${this.baseUrl}/search`, { params });
  }

  getFiles(providerCode: string, identifier: string): Observable<DiscoveryFileDto[]> {
    return this.http.get<DiscoveryFileDto[]>(
      `${this.baseUrl}/${encodeURIComponent(providerCode)}/${encodeURIComponent(identifier)}/files`
    );
  }

  importBook(request: BookImportRequest): Observable<BookDetail> {
    return this.http.post<BookDetail>(`${environment.apiUrl}/books/import`, request);
  }
}
