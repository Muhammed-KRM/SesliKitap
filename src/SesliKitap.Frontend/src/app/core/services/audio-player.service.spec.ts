import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import { BookDetail } from '../models/book.model';
import { ChapterTimestamps } from '../models/timestamps.model';
import { AudioFormatService } from './audio-format.service';
import { AudioPlayerService, findSentence } from './audio-player.service';
import { BookService } from './book.service';
import { ChapterService } from './chapter.service';
import { MediaSessionService } from './media-session.service';
import { MediaTokenService } from './media-token.service';
import { OfflineStorageService } from './offline-storage.service';
import { ProgressService } from './progress.service';
import { SignalrService } from './signalr.service';
import { SleepTimerService } from './sleep-timer.service';
import { ToastService } from './toast.service';

describe('findSentence (İkili Arama)', () => {
  const sampleTs: ChapterTimestamps = {
    version: 1,
    chapterId: 'ch-1',
    durationMs: 12000,
    sentences: [
      { i: 0, p: 0, s: 0, e: 2500, t: 'Birinci cümle.' },
      { i: 1, p: 0, s: 2500, e: 6000, t: 'İkinci cümle.' },
      { i: 2, p: 1, s: 6400, e: 12000, t: 'Üçüncü cümle.' }
    ]
  };

  it('verilen milisaniye konumuna göre doğru cümleyi bulmalıdır', () => {
    expect(findSentence(null, 1000)).toBeNull();
    expect(findSentence(sampleTs, -5)).toBeNull();
    expect(findSentence(sampleTs, 0)?.i).toBe(0);
    expect(findSentence(sampleTs, 2499)?.i).toBe(0);
    expect(findSentence(sampleTs, 2500)?.i).toBe(1);
    expect(findSentence(sampleTs, 6200)?.i).toBe(1);
    expect(findSentence(sampleTs, 8500)?.i).toBe(2);
  });
});

describe('AudioPlayerService', () => {
  let service: AudioPlayerService;
  let fakeAudio: HTMLAudioElement;
  let progressSpy: jasmine.SpyObj<ProgressService>;

  const sampleBook: BookDetail = {
    id: 'book-1',
    baslik: 'Test Kitabı',
    yazar: 'Yazar',
    dil: 'tr',
    aciklama: null,
    kaynak: 'Yukleme',
    kaynakKimligi: null,
    kaynakUrl: null,
    format: 'Pdf',
    dosyaBoyutu: 1024,
    sayfaSayisi: 10,
    toplamKelimeSayisi: 500,
    toplamSureSn: 300,
    durum: 'Hazir',
    hataMesaji: null,
    kapakVar: false,
    varsayilanSesProfiliId: null,
    bolumSayisi: 2,
    hazirBolumSayisi: 2,
    cikarimIstatistikleri: null,
    olusturmaTarihi: new Date().toISOString(),
    sonDinlenmeTarihi: null,
    bolumler: [
      {
        id: 'ch-1',
        siraNo: 1,
        baslik: '1. Bölüm',
        baslangicSayfasi: 1,
        bitisSayfasi: 5,
        kelimeSayisi: 250,
        karakterSayisi: 1500,
        tahminiSureSn: 150,
        sureSn: 150,
        dahilMi: true,
        otomatikHaricNedeni: null,
        sesDurumu: 'Hazir',
        hataMesaji: null,
        metinSurumu: 1,
        sesSurumu: 1
      },
      {
        id: 'ch-2',
        siraNo: 2,
        baslik: '2. Bölüm',
        baslangicSayfasi: 6,
        bitisSayfasi: 10,
        kelimeSayisi: 250,
        karakterSayisi: 1500,
        tahminiSureSn: 150,
        sureSn: 150,
        dahilMi: true,
        otomatikHaricNedeni: null,
        sesDurumu: 'Hazir',
        hataMesaji: null,
        metinSurumu: 1,
        sesSurumu: 1
      }
    ]
  };

  beforeEach(() => {
    progressSpy = jasmine.createSpyObj<ProgressService>('ProgressService', [
      'get',
      'reportPosition'
    ]);
    progressSpy.get.and.returnValue(of(null));
    progressSpy.reportPosition.and.resolveTo();

    TestBed.configureTestingModule({
      providers: [
        AudioPlayerService,
        SleepTimerService,
        ToastService,
        {
          provide: BookService,
          useValue: { getById: () => of(sampleBook) }
        },
        {
          provide: ChapterService,
          useValue: {
            getTimestamps: () =>
              of({
                version: 1,
                chapterId: 'ch-1',
                durationMs: 150000,
                sentences: [{ i: 0, p: 0, s: 0, e: 5000, t: 'Merhaba.' }]
              })
          }
        },
        {
          provide: MediaTokenService,
          useValue: {
            audioUrl: async (id: string) => `/api/chapters/${id}/audio?t=tok`,
            coverUrl: async () => '/api/books/book-1/cover?t=tok',
            invalidate: () => undefined
          }
        },
        {
          provide: AudioFormatService,
          useValue: { preferred: signal('opus'), defaultRate: signal(1.0) }
        },
        {
          provide: OfflineStorageService,
          useValue: {
            getAudioUrl: async () => null,
            getBookMeta: async () => null,
            getOfflineTimestamps: async () => null,
            hasAudio: async () => false
          }
        },
        {
          provide: ProgressService,
          useValue: progressSpy
        },
        {
          provide: MediaSessionService,
          useValue: {
            bindHandlers: () => undefined,
            update: () => undefined,
            setPositionState: () => undefined
          }
        },
        {
          provide: SignalrService,
          useValue: {
            joinBook: async () => undefined,
            progressUpdated$: new Subject()
          }
        }
      ]
    });

    service = TestBed.inject(AudioPlayerService);
    fakeAudio = document.createElement('audio');
    spyOn(fakeAudio, 'play').and.resolveTo();
    spyOn(fakeAudio, 'pause').and.stub();
    service.setAudioElementForTest(fakeAudio);
  });

  it('kitabı yüklemeli, hız değiştirdiğinde preservesPitch korumalı ve sonraki bölüme geçebilmelidir', async () => {
    await service.loadBook('book-1', { autoplay: true });
    expect(service.book()?.id).toBe('book-1');
    expect(service.chapter()?.id).toBe('ch-1');
    expect(service.isPlaying()).toBeTrue();

    service.setRate(1.75);
    expect(service.rate()).toBe(1.75);
    expect(fakeAudio.playbackRate).toBe(1.75);
    expect(fakeAudio.preservesPitch).toBeTrue();

    await service.nextChapter();
    expect(service.chapter()?.id).toBe('ch-2');

    // Son bölümden sonra nextChapter çağrıldığında tamamlandı olarak işaretlenmelidir
    await service.nextChapter();
    expect(progressSpy.reportPosition).toHaveBeenCalledWith(
      'book-1',
      'ch-2',
      150,
      1.75,
      true,
      true
    );
  });
});
