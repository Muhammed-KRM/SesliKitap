import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ProgressDto, ProgressUpdateRequest } from '../models/progress.model';
import { AccessKeyService } from './access-key.service';
import { SKIP_ERROR_TOAST } from './api-base.service';
import { DeviceService } from './device.service';
import { OfflineStorageService } from './offline-storage.service';
import { SignalrService } from './signalr.service';

@Injectable({ providedIn: 'root' })
export class ProgressService {
  private readonly http = inject(HttpClient);
  private readonly accessKey = inject(AccessKeyService);
  private readonly device = inject(DeviceService);
  private readonly offlineStorage = inject(OfflineStorageService);
  private readonly signalr = inject(SignalrService);

  private lastReportedBookId: string | null = null;
  private lastReportedChapterId: string | null = null;
  private lastReportedPos = -999;

  get(bookId: string): Observable<ProgressDto | null> {
    const context = new HttpContext().set(SKIP_ERROR_TOAST, true);
    return this.http
      .get<ProgressDto | null>(`${environment.apiUrl}/books/${bookId}/progress`, { context })
      .pipe(catchError(() => of(null)));
  }

  async reportPosition(
    bookId: string,
    chapterId: string,
    positionSec: number,
    rate: number,
    completed = false,
    force = false
  ): Promise<void> {
    if (
      !force &&
      !completed &&
      this.lastReportedBookId === bookId &&
      this.lastReportedChapterId === chapterId &&
      Math.abs(positionSec - this.lastReportedPos) < 1.0
    ) {
      return;
    }

    this.lastReportedBookId = bookId;
    this.lastReportedChapterId = chapterId;
    this.lastReportedPos = positionSec;

    const payload: ProgressUpdateRequest = {
      chapterId,
      pozisyonSn: Math.max(0, Math.round(positionSec * 100) / 100),
      oynatmaHizi: rate,
      cihazKimligi: this.device.deviceId(),
      cihazAdi: this.device.deviceName(),
      istemciZamani: new Date().toISOString(),
      tamamlandiMi: completed
    };

    const sent = await this.sendPutKeepalive(bookId, payload);
    if (!sent) {
      await this.offlineStorage.savePendingProgress(bookId, payload);
    } else {
      await this.offlineStorage.deletePendingProgress(bookId);
    }
  }

  async flushPending(): Promise<void> {
    const pendingList = await this.offlineStorage.getAllPendingProgress();
    for (const item of pendingList) {
      const ok = await this.sendPutKeepalive(item.bookId, item.request);
      if (ok) {
        await this.offlineStorage.deletePendingProgress(item.bookId);
      }
    }
  }

  private async sendPutKeepalive(bookId: string, payload: ProgressUpdateRequest): Promise<boolean> {
    const key = this.accessKey.key();
    if (!key) {
      return false;
    }

    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'X-API-Key': key
    };

    const connId = this.signalr.connectionId();
    if (connId) {
      headers['X-SignalR-ConnectionId'] = connId;
    }

    try {
      const resp = await fetch(`${environment.apiUrl}/books/${bookId}/progress`, {
        method: 'PUT',
        keepalive: true,
        headers,
        body: JSON.stringify(payload)
      });
      return resp.ok;
    } catch {
      return false;
    }
  }
}
