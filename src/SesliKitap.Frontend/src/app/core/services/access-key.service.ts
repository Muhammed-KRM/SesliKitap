import { HttpClient, HttpContext, HttpHeaders } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SKIP_ERROR_TOAST } from './api-base.service';

const STORAGE_KEY = 'sk.apiKey';

@Injectable({ providedIn: 'root' })
export class AccessKeyService {
  private readonly http = inject(HttpClient);
  readonly key = signal<string | null>(this.readInitialKey());
  readonly hasKey = computed(() => Boolean(this.key()?.trim()));

  private readInitialKey(): string | null {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  }

  setKey(value: string): void {
    const trimmed = value.trim();
    this.key.set(trimmed);
    try {
      localStorage.setItem(STORAGE_KEY, trimmed);
    } catch {
      // ignore storage errors
    }
  }

  clear(): void {
    this.key.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // ignore storage errors
    }
  }

  verify(candidate: string): Observable<boolean> {
    const trimmed = candidate.trim();
    if (!trimmed) {
      return of(false);
    }

    const headers = new HttpHeaders({ 'X-API-Key': trimmed });
    const context = new HttpContext().set(SKIP_ERROR_TOAST, true);

    return this.http
      .get(`${environment.apiUrl}/auth/verify`, {
        headers,
        context,
        observe: 'response'
      })
      .pipe(
        tap(() => this.setKey(trimmed)),
        map((res) => res.status === 204 || res.status === 200),
        catchError(() => of(false))
      );
  }
}
