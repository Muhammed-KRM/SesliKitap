import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { SleepTimerService } from './sleep-timer.service';

describe('SleepTimerService', () => {
  let service: SleepTimerService;
  let volumes: number[];
  let pausedCount: number;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SleepTimerService]
    });
    service = TestBed.inject(SleepTimerService);
    volumes = [];
    pausedCount = 0;

    service.registerCallbacks({
      onVolumeChange: (v) => volumes.push(v),
      onPause: () => {
        pausedCount++;
      }
    });
  });

  afterEach(() => {
    service.cancel();
  });

  it('dakika sayacı son 10 saniyede sesi kısmalı ve süre bitince duraklatmalıdır', fakeAsync(() => {
    // 0.25 dk = 15 sn
    service.startMinutes(0.25);
    expect(service.mode()).toBe('dakika');
    expect(service.remainingSec()).toBe(15);

    // İlk 5 saniye: remaining 10'a iner
    tick(5000);
    expect(service.remainingSec()).toBe(10);

    // Son 9 saniye: ses kademeli azalır
    tick(9000);
    expect(service.remainingSec()).toBe(1);
    expect(volumes.some((v) => v < 1.0 && v >= 0.05)).toBeTrue();

    // Son 1 saniye: süre biter, pause çağrılır ve ses 1.0'a döner
    tick(1000);
    expect(service.mode()).toBe('kapali');
    expect(service.remainingSec()).toBe(0);
    expect(pausedCount).toBe(1);
    expect(volumes[volumes.length - 1]).toBe(1.0);
  }));

  it('bölüm sonu modunda consumeEndOfChapterStop true dönmeli ve modu kapatmalıdır', () => {
    service.startEndOfChapter();
    expect(service.mode()).toBe('bolumSonu');
    expect(service.consumeEndOfChapterStop()).toBeTrue();
    expect(service.mode()).toBe('kapali');
    expect(service.consumeEndOfChapterStop()).toBeFalse();
  });
});
