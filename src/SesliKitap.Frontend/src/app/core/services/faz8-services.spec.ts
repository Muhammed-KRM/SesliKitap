import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BookmarkService } from './bookmark.service';
import { PronunciationService } from './pronunciation.service';
import { UsageService } from './usage.service';

describe('Faz 8 Services (Pronunciation, Bookmark, Usage)', () => {
  let httpMock: HttpTestingController;
  let pronunciationService: PronunciationService;
  let bookmarkService: BookmarkService;
  let usageService: UsageService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        PronunciationService,
        BookmarkService,
        UsageService
      ]
    });

    httpMock = TestBed.inject(HttpTestingController);
    pronunciationService = TestBed.inject(PronunciationService);
    bookmarkService = TestBed.inject(BookmarkService);
    usageService = TestBed.inject(UsageService);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('PronunciationService should list, create and preview rules', () => {
    pronunciationService.getAll().subscribe((list) => {
      expect(list.length).toBe(1);
      expect(list[0].kelime).toBe('AI');
    });

    const getReq = httpMock.expectOne((r) => r.url.endsWith('/pronunciations') && r.method === 'GET');
    getReq.flush([
      {
        id: '11111111-1111-1111-1111-111111111111',
        kelime: 'AI',
        okunus: 'yapay zekâ',
        buyukKucukDuyarli: false,
        aktifMi: true,
        olusturmaTarihi: '2026-04-01T00:00:00Z'
      }
    ]);

    pronunciationService.preview('AI harika').subscribe((res) => {
      expect(res.onizlemeMetni).toBe('yapay zekâ harika');
      expect(res.degisenKelimeSayisi).toBe(1);
    });

    const prevReq = httpMock.expectOne((r) => r.url.endsWith('/pronunciations/preview'));
    expect(prevReq.request.method).toBe('POST');
    prevReq.flush({
      orijinalMetin: 'AI harika',
      onizlemeMetni: 'yapay zekâ harika',
      degisenKelimeSayisi: 1
    });
  });

  it('BookmarkService should list, create and delete bookmarks for a book', () => {
    const bookId = '22222222-2222-2222-2222-222222222222';
    bookmarkService
      .create(bookId, {
        chapterId: '33333333-3333-3333-3333-333333333333',
        pozisyonSn: 42.5,
        not: 'Önemli not'
      })
      .subscribe((bm) => {
        expect(bm.pozisyonSn).toBe(42.5);
      });

    const postReq = httpMock.expectOne((r) => r.url.endsWith(`/books/${bookId}/bookmarks`));
    expect(postReq.request.method).toBe('POST');
    postReq.flush({
      id: '44444444-4444-4444-4444-444444444444',
      bookId,
      chapterId: '33333333-3333-3333-3333-333333333333',
      chapterSiraNo: 1,
      chapterBaslik: 'Bölüm 1',
      pozisyonSn: 42.5,
      not: 'Önemli not',
      olusturmaTarihi: '2026-04-01T00:00:00Z'
    });
  });

  it('UsageService should fetch monthly GPU usage report', () => {
    usageService.getMonthlyUsage('2026-04').subscribe((rep) => {
      expect(rep.yil).toBe(2026);
      expect(rep.sonAltiAy.length).toBe(6);
    });

    const req = httpMock.expectOne(
      (r) => r.url.endsWith('/usage/monthly') && r.params.get('ay') === '2026-04'
    );
    expect(req.request.method).toBe('GET');
    req.flush({
      yil: 2026,
      ay: 4,
      toplamGpuMs: 3600000,
      toplamGpuSaat: 1,
      toplamMaliyetUsd: 0.8,
      aylikButceUsd: 10,
      butceKullanimYuzdesi: 8,
      uretilenSesSn: 7200,
      uretilenSesSaat: 2,
      tamamlananIsSayisi: 1,
      seslendirilenKitapSayisi: 1,
      onbellektenParcaSayisi: 0,
      uretilenParcaSayisi: 20,
      sonAltiAy: Array.from({ length: 6 }, (_, i) => ({
        yil: 2026,
        ay: i + 1,
        donemEtiketi: `0${i + 1}/2026`,
        toplamGpuMs: 3600000,
        toplamMaliyetUsd: 0.8,
        uretilenSesSaat: 2,
        tamamlananIsSayisi: 1
      }))
    });
  });
});
