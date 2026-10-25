import { Injectable, inject, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { KitapDurumu, SentezIsDurumu, SesProfiliDurumu } from '../models/enums';
import { AccessKeyService } from './access-key.service';

export interface BookProcessingProgressMsg {
  bookId: string;
  asama: string;
  yuzde: number;
  mesaj: string;
}

export interface BookStatusChangedMsg {
  bookId: string;
  durum: KitapDurumu;
  bolumSayisi: number;
  hazirBolumSayisi: number;
  hataMesaji?: string | null;
}

export interface SynthesisProgressMsg {
  jobId: string;
  bookId: string;
  chapterId: string | null;
  bolumSiraNo?: number | null;
  asama?: string;
  yuzde?: number;
  genelYuzde?: number;
  bolumYuzde: number;
  tamamlananBolum?: number;
  toplamBolum?: number;
  mesaj: string;
}

export interface ChapterReadyMsg {
  jobId: string;
  bookId: string;
  chapterId: string;
  siraNo?: number;
  baslik?: string;
  sureSn: number;
  motor?: string;
}

export interface SynthesisFinishedMsg {
  jobId: string;
  bookId: string;
  durum?: SentezIsDurumu;
  sonuc?: string;
  tamamlananBolum: number;
  hataliBolum: number;
  maliyetUsd?: number;
}

export interface VoiceProfileReadyMsg {
  voiceProfileId: string;
  durum?: SesProfiliDurumu;
  basarili?: boolean;
  hataMesaji?: string | null;
}

export interface PipelineErrorMsg {
  bookId?: string | null;
  jobId?: string | null;
  chapterId?: string | null;
  voiceProfileId?: string | null;
  asama: string;
  hataKodu?: string;
  hataMesaji?: string;
  kullaniciMesaji?: string;
  kurtarilabilir?: boolean;
}

export interface ProgressUpdatedMsg {
  bookId: string;
  chapterId: string;
  bolumSiraNo: number;
  pozisyonSn: number;
  oynatmaHizi: number;
  cihazKimligi: string;
  cihazAdi?: string | null;
  istemciZamani: string;
}

export interface ExportReadyMsg {
  bookId: string;
  format: string;
  boyut?: number;
  dosyaBoyutu?: number;
  basarili?: boolean;
  basariliMi?: boolean;
  hataMesaji?: string | null;
}

export type HubConnectionStatus = 'connected' | 'reconnecting' | 'disconnected';

@Injectable({ providedIn: 'root' })
export class SignalrService {
  private readonly accessKey = inject(AccessKeyService);
  private hubConnection: signalR.HubConnection | null = null;

  readonly status = signal<HubConnectionStatus>('disconnected');
  readonly connectionId = signal<string | null>(null);
  readonly bookProcessingProgress = signal<Record<string, BookProcessingProgressMsg | undefined>>({});
  readonly synthesisProgress = signal<Record<string, SynthesisProgressMsg | undefined>>({});

  readonly bookProcessingProgress$ = new Subject<BookProcessingProgressMsg>();
  readonly bookStatusChanged$ = new Subject<BookStatusChangedMsg>();
  readonly synthesisProgress$ = new Subject<SynthesisProgressMsg>();
  readonly chapterReady$ = new Subject<ChapterReadyMsg>();
  readonly synthesisFinished$ = new Subject<SynthesisFinishedMsg>();
  readonly voiceProfileReady$ = new Subject<VoiceProfileReadyMsg>();
  readonly pipelineError$ = new Subject<PipelineErrorMsg>();
  readonly progressUpdated$ = new Subject<ProgressUpdatedMsg>();
  readonly exportReady$ = new Subject<ExportReadyMsg>();

  private readonly bookGroupCounts = new Map<string, number>();
  private libraryRefCount = 0;

  async connect(): Promise<void> {
    if (!this.accessKey.hasKey()) {
      return;
    }

    if (
      this.hubConnection &&
      (this.hubConnection.state === signalR.HubConnectionState.Connected ||
        this.hubConnection.state === signalR.HubConnectionState.Connecting ||
        this.hubConnection.state === signalR.HubConnectionState.Reconnecting)
    ) {
      return;
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl, {
        accessTokenFactory: () => this.accessKey.key() ?? ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.registerHandlers(this.hubConnection);

    this.hubConnection.onreconnecting(() => {
      this.status.set('reconnecting');
    });

    this.hubConnection.onreconnected(() => {
      this.status.set('connected');
      this.connectionId.set(this.hubConnection?.connectionId ?? null);
      void this.rejoinActiveGroups();
    });

    this.hubConnection.onclose(() => {
      this.status.set('disconnected');
      this.connectionId.set(null);
    });

    try {
      await this.hubConnection.start();
      this.status.set('connected');
      this.connectionId.set(this.hubConnection.connectionId ?? null);
      await this.rejoinActiveGroups();
    } catch {
      this.status.set('disconnected');
      setTimeout(() => {
        if (this.status() === 'disconnected' && this.accessKey.hasKey()) {
          void this.connect();
        }
      }, 3000);
    }
  }

  async disconnect(): Promise<void> {
    if (this.hubConnection) {
      try {
        await this.hubConnection.stop();
      } catch {
        // ignore
      }
      this.hubConnection = null;
    }
    this.status.set('disconnected');
    this.connectionId.set(null);
  }

  async joinBook(bookId: string): Promise<void> {
    const current = this.bookGroupCounts.get(bookId) ?? 0;
    this.bookGroupCounts.set(bookId, current + 1);
    if (current === 0 && this.isConnected()) {
      try {
        await this.hubConnection!.invoke('JoinBookGroup', bookId);
      } catch {
        // ignore
      }
    }
  }

  async leaveBook(bookId: string): Promise<void> {
    const current = this.bookGroupCounts.get(bookId) ?? 0;
    if (current <= 1) {
      this.bookGroupCounts.delete(bookId);
      if (this.isConnected()) {
        try {
          await this.hubConnection!.invoke('LeaveBookGroup', bookId);
        } catch {
          // ignore
        }
      }
    } else {
      this.bookGroupCounts.set(bookId, current - 1);
    }
  }

  async joinLibrary(): Promise<void> {
    this.libraryRefCount += 1;
    if (this.libraryRefCount === 1 && this.isConnected()) {
      try {
        await this.hubConnection!.invoke('JoinLibraryGroup');
      } catch {
        // ignore
      }
    }
  }

  async leaveLibrary(): Promise<void> {
    if (this.libraryRefCount <= 1) {
      this.libraryRefCount = 0;
      if (this.isConnected()) {
        try {
          await this.hubConnection!.invoke('LeaveLibraryGroup');
        } catch {
          // ignore
        }
      }
    } else {
      this.libraryRefCount -= 1;
    }
  }

  getBookRefCount(bookId: string): number {
    return this.bookGroupCounts.get(bookId) ?? 0;
  }

  private isConnected(): boolean {
    return this.hubConnection?.state === signalR.HubConnectionState.Connected;
  }

  private async rejoinActiveGroups(): Promise<void> {
    if (!this.isConnected()) {
      return;
    }
    if (this.libraryRefCount > 0) {
      try {
        await this.hubConnection!.invoke('JoinLibraryGroup');
      } catch {
        // ignore
      }
    }
    for (const bookId of this.bookGroupCounts.keys()) {
      try {
        await this.hubConnection!.invoke('JoinBookGroup', bookId);
      } catch {
        // ignore
      }
    }
  }

  private registerHandlers(conn: signalR.HubConnection): void {
    conn.on('BookProcessingProgress', (msg: BookProcessingProgressMsg) => {
      this.bookProcessingProgress.update((map) => ({ ...map, [msg.bookId]: msg }));
      this.bookProcessingProgress$.next(msg);
    });

    conn.on('BookStatusChanged', (msg: BookStatusChangedMsg) => {
      this.bookStatusChanged$.next(msg);
    });

    conn.on('SynthesisProgress', (msg: SynthesisProgressMsg) => {
      this.synthesisProgress.update((map) => ({ ...map, [msg.bookId]: msg }));
      this.synthesisProgress$.next(msg);
    });

    conn.on('ChapterReady', (msg: ChapterReadyMsg) => {
      this.chapterReady$.next(msg);
    });

    conn.on('SynthesisFinished', (msg: SynthesisFinishedMsg) => {
      this.synthesisFinished$.next(msg);
    });

    conn.on('SynthesisCompleted', (msg: SynthesisFinishedMsg) => {
      this.synthesisFinished$.next(msg);
    });

    conn.on('VoiceProfileReady', (msg: VoiceProfileReadyMsg) => {
      this.voiceProfileReady$.next(msg);
    });

    conn.on('PipelineError', (msg: PipelineErrorMsg) => {
      this.pipelineError$.next(msg);
    });

    conn.on('ProgressUpdated', (msg: ProgressUpdatedMsg) => {
      this.progressUpdated$.next(msg);
    });

    conn.on('ExportReady', (msg: ExportReadyMsg) => {
      this.exportReady$.next(msg);
    });
  }
}
