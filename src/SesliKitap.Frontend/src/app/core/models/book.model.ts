import { DosyaFormati, KitapDurumu, KitapKaynagi } from './enums';
import { ChapterSummary } from './chapter.model';
import { SynthesisJobDto } from './synthesis.model';

export interface ExtractionStats {
  silinenUstAltBilgiSayisi?: number;
  silinenDipnotSayisi?: number;
  birlestirilenTireliKelimeSayisi?: number;
  tespitYontemi?: string;
}

export interface BookListDto {
  id: string;
  baslik: string;
  yazar: string | null;
  durum: KitapDurumu;
  bolumSayisi: number;
  hazirBolumSayisi: number;
  toplamSureSn: number;
  tamamlanmaYuzdesi: number | null;
  kapakVar: boolean;
  olusturmaTarihi: string;
  sonDinlenmeTarihi: string | null;
}

export interface BookDetail {
  id: string;
  baslik: string;
  yazar: string | null;
  dil: string;
  aciklama: string | null;
  kaynakTipi?: KitapKaynagi;
  kaynak?: KitapKaynagi;
  dosyaFormati?: DosyaFormati;
  format?: DosyaFormati;
  dosyaBoyutu: number;
  sayfaSayisi: number | null;
  bolumSayisi?: number;
  hazirBolumSayisi?: number;
  kelimeSayisi?: number;
  toplamKelimeSayisi?: number;
  toplamSureSn: number;
  durum: KitapDurumu;
  hataMesaji: string | null;
  varsayilanSesProfiliId: string | null;
  kapakVar: boolean;
  istatistik?: ExtractionStats | null;
  cikarimIstatistikleri?: ExtractionStats | null;
  olusturmaTarihi: string;
  sonDinlenmeTarihi: string | null;
  bolumler: ChapterSummary[];
  aktifIs?: SynthesisJobDto | null;
}

export interface BookUpdateRequest {
  baslik: string;
  yazar?: string | null;
  aciklama?: string | null;
  varsayilanSesProfiliId?: string | null;
}
