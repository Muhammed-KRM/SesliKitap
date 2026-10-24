import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

interface MediaTokenResponse {
  token: string;
  sonGecerlilik: string;
}

const REFRESH_THRESHOLD_MS = 10 * 60 * 1000; // 10 dakika

@Injectable({ providedIn: 'root' })
export class MediaTokenService {
  private readonly http = inject(HttpClient);
  readonly currentToken = signal<string | null>(null);
  private expiresAtMs = 0;
  private inflight: Promise<string> | null = null;

  async getToken(forceRefresh = false): Promise<string> {
    const now = Date.now();
    const cached = this.currentToken();
    if (!forceRefresh && cached && this.expiresAtMs - now > REFRESH_THRESHOLD_MS) {
      return cached;
    }

    if (this.inflight && !forceRefresh) {
      return this.inflight;
    }

    this.inflight = firstValueFrom(
      this.http.get<MediaTokenResponse>(`${environment.apiUrl}/auth/media-token`)
    )
      .then((res) => {
        this.currentToken.set(res.token);
        const parsed = Date.parse(res.sonGecerlilik);
        this.expiresAtMs = Number.isNaN(parsed) ? Date.now() + 20 * 60 * 1000 : parsed;
        return res.token;
      })
      .finally(() => {
        this.inflight = null;
      });

    return this.inflight;
  }

  async audioUrl(chapterId: string, format: 'opus' | 'mp3' = 'opus'): Promise<string> {
    const token = await this.getToken();
    return `${environment.apiUrl}/chapters/${chapterId}/audio?format=${format}&t=${encodeURIComponent(token)}`;
  }

  async coverUrl(bookId: string): Promise<string> {
    const token = await this.getToken();
    return `${environment.apiUrl}/books/${bookId}/cover?t=${encodeURIComponent(token)}`;
  }

  async sampleUrl(voiceId: string): Promise<string> {
    const token = await this.getToken();
    return `${environment.apiUrl}/voice-profiles/${voiceId}/sample?t=${encodeURIComponent(token)}`;
  }
}
