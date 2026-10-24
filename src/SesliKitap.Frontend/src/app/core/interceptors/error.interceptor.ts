import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { T } from '../i18n/tr';
import { AccessKeyService } from '../services/access-key.service';
import { SKIP_ERROR_TOAST } from '../services/api-base.service';
import { ToastService } from '../services/toast.service';

let offlineToastShown = false;

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const accessKey = inject(AccessKeyService);
  const router = inject(Router);
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      const skipToast = req.context.get(SKIP_ERROR_TOAST);

      if (err.status === 401) {
        accessKey.clear();
        const currentUrl = router.url;
        if (!currentUrl.startsWith('/erisim')) {
          void router.navigate(['/erisim'], { queryParams: { donus: currentUrl } });
        }
        return throwError(() => err);
      }

      if (skipToast) {
        return throwError(() => err);
      }

      if (err.status === 0) {
        if (!offlineToastShown) {
          offlineToastShown = true;
          toast.warning(T.common.offlineWarning);
          setTimeout(() => {
            offlineToastShown = false;
          }, 15000);
        }
        return throwError(() => err);
      }

      const problem = err.error as
        | { title?: string; detail?: string; traceId?: string; izlemeId?: string }
        | undefined;

      if (err.status >= 500) {
        const trace = problem?.izlemeId ?? problem?.traceId ?? 'bilinmiyor';
        toast.error('Sunucu hatası', `${problem?.detail ?? 'Beklenmeyen bir hata oluştu.'} (İzleme: ${trace})`);
      } else if (err.status === 400 || err.status === 404 || err.status === 409 || err.status === 429) {
        toast.error(problem?.title ?? 'İşlem başarısız', problem?.detail ?? err.message);
      }

      return throwError(() => err);
    })
  );
};
