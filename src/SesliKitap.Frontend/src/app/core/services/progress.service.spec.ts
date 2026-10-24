import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AccessKeyService } from './access-key.service';
import { DeviceService } from './device.service';
import { OfflineStorageService } from './offline-storage.service';
import { ProgressService } from './progress.service';
import { SignalrService } from './signalr.service';

describe('ProgressService', () => {
  let service: ProgressService;
  let offlineSpy: jasmine.SpyObj<OfflineStorageService>;

  beforeEach(() => {
    offlineSpy = jasmine.createSpyObj<OfflineStorageService>('OfflineStorageService', [
      'savePendingProgress',
      'deletePendingProgress',
      'getAllPendingProgress'
    ]);
    offlineSpy.savePendingProgress.and.resolveTo();
    offlineSpy.deletePendingProgress.and.resolveTo();
    offlineSpy.getAllPendingProgress.and.resolveTo([]);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        ProgressService,
        {
          provide: AccessKeyService,
          useValue: { key: signal('test-api-key') }
        },
        {
          provide: DeviceService,
          useValue: { deviceId: signal('dev-1'), deviceName: signal('Test PC') }
        },
        {
          provide: OfflineStorageService,
          useValue: offlineSpy
        },
        {
          provide: SignalrService,
          useValue: { connectionId: signal('conn-1') }
        }
      ]
    });

    service = TestBed.inject(ProgressService);
  });

  it('ağ hatası durumunda konumu çevrimdışı kuyruğa kaydetmeli ve flushPending ile göndermelidir', async () => {
    const fetchSpy = spyOn(window, 'fetch').and.rejectWith(new Error('Network offline'));

    await service.reportPosition('book-1', 'ch-1', 42.5, 1.25);
    expect(offlineSpy.savePendingProgress).toHaveBeenCalledTimes(1);
    const savedPayload = offlineSpy.savePendingProgress.calls.mostRecent().args[1];
    expect(savedPayload.chapterId).toBe('ch-1');
    expect(savedPayload.pozisyonSn).toBe(42.5);

    offlineSpy.getAllPendingProgress.and.resolveTo([
      { bookId: 'book-1', request: savedPayload }
    ]);
    fetchSpy.and.resolveTo(new Response(null, { status: 200 }));

    await service.flushPending();
    expect(offlineSpy.deletePendingProgress).toHaveBeenCalledWith('book-1');
  });

  it('1 saniyeden küçük konum farklarında gereksiz ağ çağrısı yapmamalıdır', async () => {
    const fetchSpy = spyOn(window, 'fetch').and.resolveTo(new Response(null, { status: 200 }));

    await service.reportPosition('book-1', 'ch-1', 10.0, 1.0);
    await service.reportPosition('book-1', 'ch-1', 10.4, 1.0);

    expect(fetchSpy).toHaveBeenCalledTimes(1);
  });
});
