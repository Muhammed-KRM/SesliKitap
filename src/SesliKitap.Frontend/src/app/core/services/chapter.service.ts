import { HttpContext } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ChapterDetail,
  ChapterSummary,
  ChapterTextUpdateRequest
} from '../models/chapter.model';
import { ChapterTimestamps } from '../models/timestamps.model';
import { ApiBaseService, SKIP_ERROR_TOAST } from './api-base.service';

@Injectable({ providedIn: 'root' })
export class ChapterService extends ApiBaseService {
  getList(bookId: string): Observable<ChapterSummary[]> {
    return this.http.get<ChapterSummary[]>(this.url(`/books/${bookId}/chapters`));
  }

  getById(id: string): Observable<ChapterDetail> {
    return this.http.get<ChapterDetail>(this.url(`/chapters/${id}`));
  }

  updateText(id: string, body: ChapterTextUpdateRequest): Observable<ChapterDetail> {
    return this.http.put<ChapterDetail>(this.url(`/chapters/${id}/text`), body, {
      context: new HttpContext().set(SKIP_ERROR_TOAST, true)
    });
  }

  setInclusion(
    bookId: string,
    chapterIds: string[],
    dahilMi: boolean
  ): Observable<ChapterSummary[]> {
    return this.http.patch<ChapterSummary[]>(
      this.url(`/books/${bookId}/chapters/inclusion`),
      { chapterIds, dahilMi }
    );
  }

  reorder(bookId: string, siraliChapterIdleri: string[]): Observable<ChapterSummary[]> {
    return this.http.put<ChapterSummary[]>(
      this.url(`/books/${bookId}/chapters/reorder`),
      { siraliChapterIdleri }
    );
  }

  merge(bookId: string, chapterIds: string[], yeniBaslik?: string): Observable<ChapterSummary[]> {
    return this.http.post<ChapterSummary[]>(
      this.url(`/books/${bookId}/chapters/merge`),
      { chapterIds, yeniBaslik }
    );
  }

  split(
    id: string,
    karakterIndeksi: number,
    ikinciBolumBasligi: string
  ): Observable<ChapterSummary[]> {
    return this.http.post<ChapterSummary[]>(this.url(`/chapters/${id}/split`), {
      karakterIndeksi,
      ikinciBolumBasligi
    });
  }

  getTimestamps(id: string): Observable<ChapterTimestamps> {
    return this.http.get<ChapterTimestamps>(this.url(`/chapters/${id}/timestamps`));
  }
}
