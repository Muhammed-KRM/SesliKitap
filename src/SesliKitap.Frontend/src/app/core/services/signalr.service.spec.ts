import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SignalrService } from './signalr.service';

describe('SignalrService', () => {
  let service: SignalrService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(SignalrService);
  });

  it('joinBook ve leaveBook referans sayımını doğru yönetir', async () => {
    const bookId = '01950000-0000-7000-8000-000000000001';
    expect(service.getBookRefCount(bookId)).toBe(0);

    await service.joinBook(bookId);
    expect(service.getBookRefCount(bookId)).toBe(1);

    await service.joinBook(bookId);
    expect(service.getBookRefCount(bookId)).toBe(2);

    await service.leaveBook(bookId);
    expect(service.getBookRefCount(bookId)).toBe(1);

    await service.leaveBook(bookId);
    expect(service.getBookRefCount(bookId)).toBe(0);
  });

  it('başlangıçta bağlantı durumu disconnected olur', () => {
    expect(service.status()).toBe('disconnected');
    expect(service.connectionId()).toBeNull();
  });
});
