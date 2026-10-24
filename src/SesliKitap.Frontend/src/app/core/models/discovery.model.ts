import { DosyaFormati, KitapKaynagi } from './enums';

export interface DiscoveryFormatOption {
  format: DosyaFormati;
  indirmeUrl: string;
  boyutBayt: number | null;
}

export interface DiscoveryItem {
  kaynak: KitapKaynagi;
  kaynakKimligi: string;
  baslik: string;
  yazar: string | null;
  yayinYili: number | null;
  dil: string | null;
  kapakUrl: string | null;
  aciklama: string | null;
  mevcutFormatlar: DiscoveryFormatOption[];
  kutuphanedeVarMi: boolean;
  mevcutKitapId: string | null;
}
