import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  PronunciationCreateRequest,
  PronunciationDto,
  PronunciationPreviewResponse
} from '../models/faz8.model';
import { ApiBaseService } from './api-base.service';

@Injectable({ providedIn: 'root' })
export class PronunciationService extends ApiBaseService {
  getAll(): Observable<PronunciationDto[]> {
    return this.http.get<PronunciationDto[]>(this.url('/pronunciations'));
  }

  create(req: PronunciationCreateRequest): Observable<PronunciationDto> {
    return this.http.post<PronunciationDto>(this.url('/pronunciations'), req);
  }

  update(id: string, req: PronunciationCreateRequest): Observable<PronunciationDto> {
    return this.http.put<PronunciationDto>(this.url(`/pronunciations/${id}`), req);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(this.url(`/pronunciations/${id}`));
  }

  preview(metin: string): Observable<PronunciationPreviewResponse> {
    return this.http.post<PronunciationPreviewResponse>(this.url('/pronunciations/preview'), {
      metin
    });
  }
}
