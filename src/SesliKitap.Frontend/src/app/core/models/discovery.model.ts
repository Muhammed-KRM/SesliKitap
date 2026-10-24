import { DosyaFormati, KitapKaynagi } from './enums';

export interface DiscoveryProviderDto {
  kod: string;
  ad: string;
  kaynakTipi: KitapKaynagi;
  etkinMi: boolean;
  telifUyarisiGerekliMi: boolean;
}

export interface DiscoveryFileDto {
  dosyaAdi: string;
  format: DosyaFormati;
  boyutBayt: number | null;
  indirmeUrl: string;
}

export interface DiscoverySourceBadgeDto {
  saglayiciKodu: string;
  kaynakTipi: KitapKaynagi;
  kaynakKimligi: string;
  sayfaUrl: string | null;
}

export interface DiscoveryResultDto {
  saglayiciKodu: string;
  kaynakTipi: KitapKaynagi;
  kaynakKimligi: string;
  baslik: string;
  yazar: string | null;
  yayinYili: number | null;
  dil: string;
  kapakUrl: string | null;
  sayfaUrl: string | null;
  indirmeSayisi: number;
  mevcutFormatlar: DosyaFormati[];
  dosyalar: DiscoveryFileDto[];
  kaynaklar: DiscoverySourceBadgeDto[];
  puan: number;
  dogrulanmamisWebKaynagi: boolean;
  kutuphanedeVar: boolean;
  mevcutBookId: string | null;
}

export interface BookImportRequest {
  kaynakTipi: KitapKaynagi;
  kaynakKimligi: string;
  indirmeUrl: string;
  dosyaFormati: DosyaFormati;
  baslik: string;
  yazar?: string | null;
  yayinYili?: number | null;
  kapakUrl?: string | null;
}
