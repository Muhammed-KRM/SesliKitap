import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  let service: ToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ToastService);
  });

  it('success bildirimi ekler ve süre sonunda otomatik kaldırır', fakeAsync(() => {
    const id = service.success('İşlem başarılı', 'Detay metni');
    expect(service.toasts().length).toBe(1);
    expect(service.toasts()[0].id).toBe(id);
    expect(service.toasts()[0].type).toBe('basari');

    tick(4000);
    expect(service.toasts().length).toBe(0);
  }));

  it('dismiss belirli bildirimi listeden siler', () => {
    const id1 = service.info('Bilgi 1', undefined, undefined);
    const id2 = service.warning('Uyarı 2', undefined, undefined);
    expect(service.toasts().length).toBe(2);

    service.dismiss(id1);
    expect(service.toasts().length).toBe(1);
    expect(service.toasts()[0].id).toBe(id2);
  });
});
