import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { MonthlyUsageDto } from '../models/faz8.model';
import { ApiBaseService } from './api-base.service';

@Injectable({ providedIn: 'root' })
export class UsageService extends ApiBaseService {
  getMonthlyUsage(ay?: string): Observable<MonthlyUsageDto> {
    const params = this.buildParams(ay ? { ay } : {});
    return this.http.get<MonthlyUsageDto>(this.url('/usage/monthly'), { params });
  }
}
