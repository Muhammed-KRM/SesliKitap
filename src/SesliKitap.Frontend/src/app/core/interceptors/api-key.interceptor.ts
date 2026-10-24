import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AccessKeyService } from '../services/access-key.service';

export const apiKeyInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api')) {
    return next(req);
  }

  if (req.headers.has('X-API-Key')) {
    return next(req);
  }

  const accessKey = inject(AccessKeyService).key();
  if (!accessKey) {
    return next(req);
  }

  return next(
    req.clone({
      setHeaders: {
        'X-API-Key': accessKey
      }
    })
  );
};
