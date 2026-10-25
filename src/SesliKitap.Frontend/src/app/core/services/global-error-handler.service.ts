import { ErrorHandler, Injectable, Injector, inject } from '@angular/core';
import { ToastService } from './toast.service';

@Injectable()
export class GlobalErrorHandlerService implements ErrorHandler {
  private readonly injector = inject(Injector);

  handleError(error: unknown): void {
    console.error('[SesliKitap GlobalErrorHandler]', error);
    try {
      const toast = this.injector.get(ToastService);
      toast.error('Beklenmeyen bir istemci hatası oluştu.');
    } catch {
      // ToastService henüz hazır değilse yutulur
    }
  }
}
