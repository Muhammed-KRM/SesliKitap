import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  output,
  signal
} from '@angular/core';

@Component({
  selector: 'app-voice-recorder',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="rounded-2xl bg-slate-50 dark:bg-surface-900 border border-slate-200 dark:border-slate-800 p-4 space-y-3"
    >
      <div class="flex items-center justify-between">
        <span class="text-xs font-semibold text-slate-400">
          Mikrofon Kaydı (5 – 30 sn)
        </span>
        <span class="font-mono text-sm font-bold text-brand-400">
          {{ elapsedSec() }} sn
        </span>
      </div>

      <!-- Canlı Seviye Çubuğu -->
      <div class="h-3 w-full rounded-full bg-slate-200 dark:bg-slate-800 overflow-hidden">
        <div
          class="h-full bg-brand-500 transition-all duration-75"
          [style.width.%]="levelPercent()"
        ></div>
      </div>

      @if (errorMsg()) {
        <p class="text-xs text-rose-400">{{ errorMsg() }}</p>
      }

      <div class="flex items-center gap-2.5">
        @if (!recording()) {
          <button
            type="button"
            (click)="startRecording()"
            class="px-4 py-2 min-h-[40px] rounded-xl bg-rose-600 hover:bg-rose-500 text-white text-xs font-semibold transition"
          >
            ● Kaydı Başlat
          </button>
        } @else {
          <button
            type="button"
            (click)="stopRecording()"
            class="px-4 py-2 min-h-[40px] rounded-xl bg-slate-800 hover:bg-slate-700 text-white text-xs font-semibold transition"
          >
            ■ Kaydı Bitir
          </button>
        }

        @if (previewUrl()) {
          <audio [src]="previewUrl()!" controls class="h-9 flex-1"></audio>
        }
      </div>
    </div>
  `
})
export class VoiceRecorderComponent {
  private readonly destroyRef = inject(DestroyRef);

  readonly recorded = output<File>();

  readonly recording = signal(false);
  readonly elapsedSec = signal(0);
  readonly levelPercent = signal(0);
  readonly previewUrl = signal<string | null>(null);
  readonly errorMsg = signal<string | null>(null);

  private mediaRecorder: MediaRecorder | null = null;
  private stream: MediaStream | null = null;
  private audioCtx: AudioContext | null = null;
  private timerId: ReturnType<typeof setInterval> | null = null;
  private rafId: number | null = null;
  private chunks: Blob[] = [];

  constructor() {
    this.destroyRef.onDestroy(() => this.cleanup());
  }

  async startRecording(): Promise<void> {
    this.errorMsg.set(null);
    if (typeof navigator === 'undefined' || !navigator.mediaDevices?.getUserMedia) {
      this.errorMsg.set('Tarayıcınız mikrofon kaydını desteklemiyor.');
      return;
    }

    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: false,
          noiseSuppression: true,
          autoGainControl: false
        }
      });

      const mimeType =
        typeof MediaRecorder !== 'undefined' &&
        MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
          ? 'audio/webm;codecs=opus'
          : typeof MediaRecorder !== 'undefined' && MediaRecorder.isTypeSupported('audio/mp4')
            ? 'audio/mp4'
            : '';

      this.chunks = [];
      this.mediaRecorder = mimeType
        ? new MediaRecorder(this.stream, { mimeType })
        : new MediaRecorder(this.stream);

      this.mediaRecorder.ondataavailable = (ev) => {
        if (ev.data.size > 0) {
          this.chunks.push(ev.data);
        }
      };

      this.mediaRecorder.onstop = () => {
        const blobType = this.mediaRecorder?.mimeType || 'audio/webm';
        const ext = blobType.includes('mp4') ? 'm4a' : 'webm';
        const blob = new Blob(this.chunks, { type: blobType });
        const file = new File([blob], `mikrofon-kaydi.${ext}`, { type: blobType });
        if (this.previewUrl()) {
          URL.revokeObjectURL(this.previewUrl()!);
        }
        this.previewUrl.set(URL.createObjectURL(blob));
        this.recorded.emit(file);
        this.cleanupStream();
      };

      this.setupLevelMeter(this.stream);
      this.mediaRecorder.start(200);
      this.recording.set(true);
      this.elapsedSec.set(0);

      this.timerId = setInterval(() => {
        const next = this.elapsedSec() + 1;
        this.elapsedSec.set(next);
        if (next >= 30) {
          this.stopRecording();
        }
      }, 1000);
    } catch {
      this.errorMsg.set('Mikrofon erişim izni alınamadı.');
    }
  }

  stopRecording(): void {
    if (this.mediaRecorder && this.recording()) {
      this.recording.set(false);
      this.mediaRecorder.stop();
    }
    if (this.timerId) {
      clearInterval(this.timerId);
      this.timerId = null;
    }
  }

  private setupLevelMeter(stream: MediaStream): void {
    try {
      this.audioCtx = new AudioContext();
      const source = this.audioCtx.createMediaStreamSource(stream);
      const analyser = this.audioCtx.createAnalyser();
      analyser.fftSize = 256;
      source.connect(analyser);
      const data = new Uint8Array(analyser.frequencyBinCount);

      const tick = () => {
        if (!this.recording()) return;
        analyser.getByteFrequencyData(data);
        let sum = 0;
        for (let i = 0; i < data.length; i++) {
          sum += data[i];
        }
        const avg = sum / data.length;
        this.levelPercent.set(Math.min(100, Math.round((avg / 128) * 100)));
        this.rafId = requestAnimationFrame(tick);
      };
      this.rafId = requestAnimationFrame(tick);
    } catch {
      // ignore analyser errors
    }
  }

  private cleanupStream(): void {
    if (this.rafId !== null) {
      cancelAnimationFrame(this.rafId);
      this.rafId = null;
    }
    if (this.audioCtx) {
      void this.audioCtx.close();
      this.audioCtx = null;
    }
    if (this.stream) {
      this.stream.getTracks().forEach((t) => t.stop());
      this.stream = null;
    }
    this.levelPercent.set(0);
  }

  private cleanup(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
    }
    this.cleanupStream();
    if (this.previewUrl()) {
      URL.revokeObjectURL(this.previewUrl()!);
    }
  }
}
