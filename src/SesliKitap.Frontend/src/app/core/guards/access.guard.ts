import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AccessKeyService } from '../services/access-key.service';

export const accessGuard: CanActivateFn = (_route, state) => {
  const accessKey = inject(AccessKeyService);
  const router = inject(Router);

  if (accessKey.hasKey()) {
    return true;
  }

  return router.createUrlTree(['/erisim'], {
    queryParams: { donus: state.url }
  });
};
