export interface PronunciationDto {
  id: string;
  kaynakIfade: string;
  okunus: string;
  yalnizcaTamKelime: boolean;
  buyukKucukHarfDuyarli: boolean;
  olusturulmaTarihi: string;
}

export interface PronunciationCreateRequest {
  kaynakIfade: string;
  okunus: string;
  yalnizcaTamKelime: boolean;
  buyukKucukHarfDuyarli: boolean;
}

export interface PronunciationPreviewResponse {
  donusturulmusMetin: string;
  uygulananKuralSayisi: number;
}

export interface BookmarkDto {
  id: string;
  bookId: string;
  chapterId: string;
  bolumSiraNo: number;
  bolumBaslik: string;
  pozisyonSn: number;
  notMetni: string | null;
  cumleMetni: string | null;
  olusturulmaTarihi: string;
}

export interface BookmarkCreateRequest {
  chapterId: string;
  pozisyonSn: number;
  notMetni?: string | null;
  cumleMetni?: string | null;
}

export interface MonthlyUsageHistoryItemDto {
  ay: string;
  gpuSaat: number;
  maliyetUsd: number;
}

export interface MonthlyUsageDto {
  ay: string;
  toplamGpuSn: number;
  toplamGpuSaat: number;
  tahminiMaliyetUsd: number;
  aylikLimitUsd: number;
  limitAsildiMi: boolean;
  seslendirilenKitapSayisi: number;
  seslendirilenBolumSayisi: number;
  toplamSesSuresiSn: number;
  sonAltiAyGecmisi: MonthlyUsageHistoryItemDto[];
}
