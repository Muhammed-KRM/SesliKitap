import { BolumSesDurumu, SentezIsDurumu } from './enums';

export interface SynthesisSettings {
  cumleArasiMs: number;
  paragrafArasiMs: number;
  baslikSonrasiMs: number;
  diyalogMs: number;
  bolumBasligiOku: boolean;
  opusBitrateKbps: number;
  mp3BitrateKbps: number;
  hedefLufs: number;
}

export interface SynthesisEstimateDto {
  bolumSayisi: number;
  kelimeSayisi: number;
  tahminiSesSn: number;
  tahminiGpuSn: number;
  tahminiMaliyetUsd: number;
  buAyHarcananUsd: number;
  aylikButceUsd: number;
  kalanButceUsd: number;
  butceAsiliyor: boolean;
  onbellektekiTahminiParcaOrani: number;
}

export interface SynthesisStartRequest {
  voiceProfileId: string;
  chapterIds?: string[] | null;
  sadeceEksikler: boolean;
  force: boolean;
  cumleArasiMs?: number;
  paragrafArasiMs?: number;
}

export interface SynthesisJobDto {
  id: string;
  bookId: string;
  kitapBaslik: string;
  voiceProfileId: string;
  sesProfiliAd: string;
  durum: SentezIsDurumu;
  yuzde: number;
  toplamBolum: number;
  tamamlananBolum: number;
  hataliBolum: number;
  mevcutBolumId: string | null;
  mesaj: string | null;
  motorSirasi?: string;
  kullanilanMotorlar: string | null;
  gpuMs: number;
  tahminiMaliyetUsd: number;
  tahminiSureSn: number | null;
  uretilenSesSn: number;
  onbellektenParca: number;
  uretilenParca: number;
  hataMesaji: string | null;
  olusturmaTarihi: string;
  baslangicZamani: string | null;
  bitisZamani: string | null;
}

export interface SynthesisJobChapterDto {
  chapterId: string;
  sira: number;
  bolumSiraNo: number;
  baslik: string;
  durum: BolumSesDurumu;
  gpuMs: number;
  sureSn: number | null;
  hataMesaji: string | null;
}

export interface SynthesisJobDetailDto {
  is: SynthesisJobDto;
  ayarlar: SynthesisSettings;
  bolumler: SynthesisJobChapterDto[];
}
