import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MediaTokenService } from './media-token.service';

describe('MediaTokenService', () => {
  let service: MediaTokenService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), MediaTokenService]
    });

    service = TestBed.inject(MediaTokenService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('geçerli token önbellekteyken yeniden HTTP isteği atmamalıdır', async () => {
    const futureIso = new Date(Date.now() + 30 * 60 * 1000).toISOString();

    const p1 = service.getToken();
    const req = httpMock.expectOne('/api/auth/media-token');
    req.flush({ token: 'tok-123', sonGecerlilik: futureIso });
    const t1 = await p1;
    expect(t1).toBe('tok-123');

    const t2 = await service.getToken();
    expect(t2).toBe('tok-123');
    httpMock.expectNone('/api/auth/media-token');
  });

  it('invalidate çağrıldığında veya forceRefresh=true olduğunda yeni token almalıdır', async () => {
    const futureIso = new Date(Date.now() + 30 * 60 * 1000).toISOString();

    const p1 = service.getToken();
    httpMock.expectOne('/api/auth/media-token').flush({
      token: 'tok-old',
      sonGecerlilik: futureIso
    });
    await p1;

    service.invalidate();

    const p2 = service.audioUrl('ch-1', 'opus');
    httpMock.expectOne('/api/auth/media-token').flush({
      token: 'tok-new',
      sonGecerlilik: futureIso
    });
    const url = await p2;
    expect(url).toBe('/api/chapters/ch-1/audio?format=opus&t=tok-new');
  });
});
