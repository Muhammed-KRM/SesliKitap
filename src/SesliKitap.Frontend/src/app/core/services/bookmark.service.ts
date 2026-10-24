import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BookmarkCreateRequest, BookmarkDto } from '../models/faz8.model';
import { ApiBaseService } from './api-base.service';

@Injectable({ providedIn: 'root' })
export class BookmarkService extends ApiBaseService {
  getByBook(bookId: string): Observable<BookmarkDto[]> {
    return this.http.get<BookmarkDto[]>(this.url(`/books/${bookId}/bookmarks`));
  }

  create(bookId: string, req: BookmarkCreateRequest): Observable<BookmarkDto> {
    return this.http.post<BookmarkDto>(this.url(`/books/${bookId}/bookmarks`), req);
  }

  delete(bookId: string, bookmarkId: string): Observable<void> {
    return this.http.delete<void>(this.url(`/books/${bookId}/bookmarks/${bookmarkId}`));
  }
}
