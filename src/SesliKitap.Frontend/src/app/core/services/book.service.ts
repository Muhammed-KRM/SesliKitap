import { HttpContext, HttpEventType, HttpRequest } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, filter, map } from 'rxjs';
import { BookDetail, BookListDto, BookUpdateRequest } from '../models/book.model';
import { KitapDurumu } from '../models/enums';
import { PagedResult } from '../models/paged-result.model';
import { ApiBaseService, SKIP_ERROR_TOAST } from './api-base.service';

export interface BookListQuery {
  arama?: string;
  durum?: KitapDurumu | '';
  siralama?: string;
  sayfa?: number;
  boyut?: number;
}

export interface UploadState {
  yuzde: number;
  tamamlandi: boolean;
  sonuc?: BookDetail;
}

@Injectable({ providedIn: 'root' })
export class BookService extends ApiBaseService {
  getList(query: BookListQuery = {}): Observable<PagedResult<BookListDto>> {
    const params = this.buildParams(query as Record<string, unknown>);
    return this.http.get<PagedResult<BookListDto>>(this.url('/books'), { params });
  }

  getContinue(adet = 10): Observable<BookListDto[]> {
    const params = this.buildParams({ adet });
    return this.http.get<BookListDto[]>(this.url('/books/continue'), { params });
  }

  getById(id: string): Observable<BookDetail> {
    return this.http.get<BookDetail>(this.url(`/books/${id}`));
  }

  upload(
    file: File,
    opts?: { baslik?: string; yazar?: string }
  ): Observable<UploadState> {
    const form = new FormData();
    form.append('dosya', file, file.name);
    if (opts?.baslik?.trim()) {
      form.append('baslik', opts.baslik.trim());
    }
    if (opts?.yazar?.trim()) {
      form.append('yazar', opts.yazar.trim());
    }

    const req = new HttpRequest('POST', this.url('/books/upload'), form, {
      reportProgress: true,
      context: new HttpContext().set(SKIP_ERROR_TOAST, true)
    });

    return this.http.request<BookDetail>(req).pipe(
      filter(
        (ev) =>
          ev.type === HttpEventType.UploadProgress || ev.type === HttpEventType.Response
      ),
      map((ev) => {
        if (ev.type === HttpEventType.UploadProgress) {
          const total = ev.total ?? file.size;
          const pct = total > 0 ? Math.min(99, Math.round((ev.loaded / total) * 100)) : 0;
          return { yuzde: pct, tamamlandi: false };
        }
        return {
          yuzde: 100,
          tamamlandi: true,
          sonuc: (ev as { body?: BookDetail }).body ?? undefined
        };
      })
    );
  }

  update(id: string, body: BookUpdateRequest): Observable<BookDetail> {
    return this.http.put<BookDetail>(this.url(`/books/${id}`), body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(this.url(`/books/${id}`));
  }

  reprocess(id: string): Observable<void> {
    return this.http.post<void>(this.url(`/books/${id}/reprocess`), {});
  }

  startOcr(id: string): Observable<void> {
    return this.http.post<void>(this.url(`/books/${id}/ocr`), {});
  }
}
