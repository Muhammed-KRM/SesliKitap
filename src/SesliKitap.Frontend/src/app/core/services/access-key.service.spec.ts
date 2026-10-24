import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AccessKeyService } from './access-key.service';

describe('AccessKeyService', () => {
  let service: AccessKeyService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AccessKeyService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('başlangıçta anahtar olmadığında hasKey false döner', () => {
    expect(service.key()).toBeNull();
    expect(service.hasKey()).toBeFalse();
  });

  it('setKey anahtarı kırparak localStorage ve sinyale yazar', () => {
    service.setKey('  test-anahtari-123  ');
    expect(service.key()).toBe('test-anahtari-123');
    expect(service.hasKey()).toBeTrue();
    expect(localStorage.getItem('sk.apiKey')).toBe('test-anahtari-123');
  });

  it('clear anahtarı temizler', () => {
    service.setKey('anahtar');
    service.clear();
    expect(service.key()).toBeNull();
    expect(service.hasKey()).toBeFalse();
    expect(localStorage.getItem('sk.apiKey')).toBeNull();
  });

  it('verify 204 yanıtında true döner ve anahtarı kaydeder', () => {
    let result: boolean | undefined;
    service.verify('gecerli-anahtar').subscribe((ok) => (result = ok));

    const req = httpMock.expectOne('/api/auth/verify');
    expect(req.request.headers.get('X-API-Key')).toBe('gecerli-anahtar');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(result).toBeTrue();
    expect(service.key()).toBe('gecerli-anahtar');
  });

  it('verify 401 yanıtında false döner ve anahtarı kaydetmez', () => {
    let result: boolean | undefined;
    service.verify('yanlis-anahtar').subscribe((ok) => (result = ok));

    const req = httpMock.expectOne('/api/auth/verify');
    req.flush({ detail: 'Yetkisiz' }, { status: 401, statusText: 'Unauthorized' });

    expect(result).toBeFalse();
    expect(service.hasKey()).toBeFalse();
  });
});
