import { BolumSesDurumu } from './enums';

export interface ChapterSummary {
  id: string;
  siraNo: number;
  baslik: string;
  baslangicSayfasi: number | null;
  bitisSayfasi: number | null;
  kelimeSayisi: number;
  karakterSayisi: number;
  tahminiSureSn: number;
  sureSn: number | null;
  dahilMi: boolean;
  otomatikHaricNedeni: string | null;
  sesDurumu: BolumSesDurumu;
  metinSurumu: number;
  sesSurumu: number | null;
  hataMesaji: string | null;
}

export interface ChapterDetail extends ChapterSummary {
  bookId: string;
  temizMetin: string;
  kullaniciDuzenlediMi: boolean;
}

export interface ChapterTextUpdateRequest {
  baslik: string;
  temizMetin: string;
  beklenenMetinSurumu: number;
}
