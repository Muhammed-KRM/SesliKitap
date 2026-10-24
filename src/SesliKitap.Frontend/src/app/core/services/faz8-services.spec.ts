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
      expect(list[0].kaynakIfade).toBe('AI');
    });

    const getReq = httpMock.expectOne((r) => r.url.endsWith('/pronunciations') && r.method === 'GET');
    getReq.flush([
      {
        id: '11111111-1111-1111-1111-111111111111',
        kaynakIfade: 'AI',
        okunus: 'yapay zekâ',
        yalnizcaTamKelime: true,
        buyukKucukHarfDuyarli: false,
        olusturulmaTarihi: '2026-04-01T00:00:00Z'
      }
    ]);

    pronunciationService.preview('AI harika').subscribe((res) => {
      expect(res.donusturulmusMetin).toBe('yapay zekâ harika');
      expect(res.uygulananKuralSayisi).toBe(1);
    });

    const prevReq = httpMock.expectOne((r) => r.url.endsWith('/pronunciations/preview'));
    expect(prevReq.request.method).toBe('POST');
    prevReq.flush({ donusturulmusMetin: 'yapay zekâ harika', uygulananKuralSayisi: 1 });
  });

  it('BookmarkService should list, create and delete bookmarks for a book', () => {
    const bookId = '22222222-2222-2222-2222-222222222222';
    bookmarkService
      .create(bookId, {
        chapterId: '33333333-3333-3333-3333-333333333333',
        pozisyonSn: 42.5,
        notMetni: 'Önemli not'
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
      bolumSiraNo: 1,
      bolumBaslik: 'Bölüm 1',
      pozisyonSn: 42.5,
      notMetni: 'Önemli not',
      cumleMetni: null,
      olusturulmaTarihi: '2026-04-01T00:00:00Z'
    });
  });

  it('UsageService should fetch monthly GPU usage report', () => {
    usageService.getMonthlyUsage('2026-04').subscribe((rep) => {
      expect(rep.ay).toBe('2026-04');
      expect(rep.sonAltiAyGecmisi.length).toBe(6);
    });

    const req = httpMock.expectOne(
      (r) => r.url.endsWith('/usage/monthly') && r.params.get('ay') === '2026-04'
    );
    expect(req.request.method).toBe('GET');
    req.flush({
      ay: '2026-04',
      toplamGpuSn: 3600,
      toplamGpuSaat: 1,
      tahminiMaliyetUsd: 0.8,
      aylikLimitUsd: 10,
      limitAsildiMi: false,
      seslendirilenKitapSayisi: 1,
      seslendirilenBolumSayisi: 5,
      toplamSesSuresiSn: 7200,
      sonAltiAyGecmisi: Array.from({ length: 6 }, (_, i) => ({
        ay: `2026-0${i + 1}`,
        gpuSaat: 1,
        maliyetUsd: 0.8
      }))
    });
  });
});
