import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DiscoveryService } from './discovery.service';
import { BookImportRequest } from '../models/discovery.model';

describe('DiscoveryService', () => {
  let service: DiscoveryService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [DiscoveryService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(DiscoveryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('calls GET /api/discovery/search with query and providers', () => {
    service.search('Sefiller', ['ia', 'gutenberg'], 1).subscribe((res) => {
      expect(res.length).toBe(1);
      expect(res[0].baslik).toBe('Sefiller');
    });

    const req = httpMock.expectOne(
      (r) =>
        r.url === '/api/discovery/search' &&
        r.params.get('q') === 'Sefiller' &&
        r.params.get('kaynaklar') === 'ia,gutenberg' &&
        r.params.get('sayfa') === '1'
    );
    expect(req.request.method).toBe('GET');
    req.flush([
      {
        saglayiciKodu: 'gutenberg',
        kaynakKimligi: '135',
        baslik: 'Sefiller',
        yazar: 'Victor Hugo',
        dil: 'tr',
        telifDurumu: 'kamu_mali',
        alakaPuani: 95,
        kutuphanedeMevcutMu: false,
        dosyalar: []
      }
    ]);
  });

  it('calls POST /api/books/import with BookImportRequest payload', () => {
    const payload: BookImportRequest = {
      kaynakTipi: 'ProjectGutenberg',
      kaynakKimligi: '135',
      indirmeUrl: 'https://www.gutenberg.org/ebooks/135.epub.noimages',
      dosyaFormati: 'Epub',
      baslik: 'Sefiller',
      yazar: 'Victor Hugo'
    };

    service.importBook(payload).subscribe((book) => {
      expect(book.baslik).toBe('Sefiller');
    });

    const req = httpMock.expectOne('/api/books/import');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(payload);
    req.flush({ id: 'book-135', baslik: 'Sefiller' });
  });
});
