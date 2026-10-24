import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { SentezIsDurumu } from '../models/enums';
import { PagedResult } from '../models/paged-result.model';
import {
  SynthesisEstimateDto,
  SynthesisJobDetailDto,
  SynthesisJobDto,
  SynthesisStartRequest
} from '../models/synthesis.model';
import { ApiBaseService } from './api-base.service';

@Injectable({ providedIn: 'root' })
export class SynthesisService extends ApiBaseService {
  estimate(
    bookId: string,
    voiceProfileId: string,
    opts?: { chapterIds?: string[]; sadeceEksikler?: boolean }
  ): Observable<SynthesisEstimateDto> {
    const params = this.buildParams({
      voiceProfileId,
      chapterIds: opts?.chapterIds,
      sadeceEksikler: opts?.sadeceEksikler ?? true
    });
    return this.http.get<SynthesisEstimateDto>(
      this.url(`/books/${bookId}/synthesis/estimate`),
      { params }
    );
  }

  start(bookId: string, request: SynthesisStartRequest): Observable<SynthesisJobDto> {
    return this.http.post<SynthesisJobDto>(
      this.url(`/books/${bookId}/synthesis`),
      request
    );
  }

  getList(opts?: {
    durum?: SentezIsDurumu | '';
    sayfa?: number;
    boyut?: number;
  }): Observable<PagedResult<SynthesisJobDto>> {
    const params = this.buildParams({
      durum: opts?.durum,
      sayfa: opts?.sayfa ?? 1,
      boyut: opts?.boyut ?? 20
    });
    return this.http.get<PagedResult<SynthesisJobDto>>(this.url('/synthesis'), {
      params
    });
  }

  getById(jobId: string): Observable<SynthesisJobDetailDto> {
    return this.http.get<SynthesisJobDetailDto>(this.url(`/synthesis/${jobId}`));
  }

  cancel(jobId: string): Observable<void> {
    return this.http.post<void>(this.url(`/synthesis/${jobId}/cancel`), {});
  }

  retry(jobId: string): Observable<SynthesisJobDto> {
    return this.http.post<SynthesisJobDto>(this.url(`/synthesis/${jobId}/retry`), {});
  }
}
