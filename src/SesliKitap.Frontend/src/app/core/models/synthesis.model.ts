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
  bookId: string;
  voiceProfileId: string;
  seciliBolumSayisi: number;
  toplamKelimeSayisi: number;
  toplamKarakterSayisi: number;
  tahminiSesSureSn: number;
  tahminiGpuSureSn: number;
  tahminiMaliyetUsd: number;
  buAyHarcananUsd: number;
  aylikButceUsd: number;
  butceAsiliyorMu: boolean;
}

export interface SynthesisStartRequest {
  voiceProfileId: string;
  chapterIds?: string[] | null;
  sadeceEksikler: boolean;
  force: boolean;
}

export interface SynthesisJobDto {
  id: string;
  bookId: string;
  kitapBasligi: string;
  voiceProfileId: string;
  sesProfiliAdi: string;
  durum: SentezIsDurumu;
  yuzde: number;
  mesaj: string | null;
  mevcutBolumId: string | null;
  toplamBolumSayisi: number;
  tamamlananBolumSayisi: number;
  hataliBolumSayisi: number;
  toplamKelimeSayisi: number;
  tahminiSesSureSn: number;
  gercekSesSureSn: number;
  tahminiGpuSureSn: number;
  gercekGpuSureSn: number;
  tahminiMaliyetUsd: number;
  kullanilanMotorlar: string | null;
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
