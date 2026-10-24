export type KitapDurumu =
  | 'Indiriliyor'
  | 'Isleniyor'
  | 'OnizlemeBekliyor'
  | 'Seslendiriliyor'
  | 'KismenHazir'
  | 'Hazir'
  | 'OcrGerekli'
  | 'Hata';

export type KitapKaynagi =
  | 'Yukleme'
  | 'InternetArchive'
  | 'OpenLibrary'
  | 'ProjectGutenberg'
  | 'WebArama';

export type DosyaFormati = 'Pdf' | 'Epub' | 'Txt';

export type BolumSesDurumu =
  | 'Yok'
  | 'Kuyrukta'
  | 'Seslendiriliyor'
  | 'Hazir'
  | 'GuncelDegil'
  | 'Hata';

export type SesProfiliDurumu = 'Isleniyor' | 'Hazir' | 'Hata';

export type SesCinsiyeti = 'Belirtilmemis' | 'Erkek' | 'Kadin';

export type SentezIsDurumu =
  | 'Kuyrukta'
  | 'Calisiyor'
  | 'Tamamlandi'
  | 'KismiTamamlandi'
  | 'IptalEdildi'
  | 'Hata';
