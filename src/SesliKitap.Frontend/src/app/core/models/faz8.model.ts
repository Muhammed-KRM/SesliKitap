export interface PronunciationDto {
  id: string;
  kelime: string;
  okunus: string;
  buyukKucukDuyarli: boolean;
  aktifMi: boolean;
  aciklama?: string | null;
  olusturmaTarihi: string;
}

export interface PronunciationCreateRequest {
  kelime: string;
  okunus: string;
  buyukKucukDuyarli: boolean;
  aktifMi?: boolean;
  aciklama?: string | null;
}

export interface PronunciationPreviewResponse {
  orijinalMetin: string;
  onizlemeMetni: string;
  degisenKelimeSayisi: number;
}

export interface BookmarkDto {
  id: string;
  bookId: string;
  chapterId: string;
  chapterSiraNo: number;
  chapterBaslik: string;
  pozisyonSn: number;
  not: string | null;
  olusturmaTarihi: string;
}

export interface BookmarkCreateRequest {
  chapterId: string;
  pozisyonSn: number;
  not?: string | null;
}

export interface MonthlyUsageHistoryItemDto {
  yil: number;
  ay: number;
  donemEtiketi: string;
  toplamGpuMs: number;
  toplamMaliyetUsd: number;
  uretilenSesSaat: number;
  tamamlananIsSayisi: number;
}

export interface MonthlyUsageDto {
  yil: number;
  ay: number;
  toplamGpuMs: number;
  toplamGpuSaat: number;
  toplamMaliyetUsd: number;
  aylikButceUsd: number;
  butceKullanimYuzdesi: number;
  uretilenSesSn: number;
  uretilenSesSaat: number;
  tamamlananIsSayisi: number;
  seslendirilenKitapSayisi: number;
  onbellektenParcaSayisi: number;
  uretilenParcaSayisi: number;
  sonAltiAy: MonthlyUsageHistoryItemDto[];
}
