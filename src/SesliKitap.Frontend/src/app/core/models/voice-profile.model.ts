import { SesCinsiyeti, SesProfiliDurumu } from './enums';

export interface VoiceParameters {
  cfgValue: number;
  inferenceTimesteps: number;
  konusmaHizi: number;
  tohum?: number | null;
}

export interface VoiceProfileDto {
  id: string;
  ad: string;
  aciklama: string | null;
  cinsiyet: SesCinsiyeti | null;
  referansSureSn: number | null;
  referansMetin: string | null;
  durum: SesProfiliDurumu;
  hataMesaji: string | null;
  varsayilanMi: boolean;
  parametreler: VoiceParameters;
  ornekSesVar: boolean;
  ornekVar?: boolean;
  olusturmaTarihi: string;
}

export interface VoiceProfileUpdateRequest {
  ad: string;
  aciklama?: string | null;
  cinsiyet: SesCinsiyeti;
  referansMetin?: string | null;
  parametreler: VoiceParameters;
}
