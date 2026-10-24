export interface ProgressDto {
  bookId: string;
  chapterId: string;
  bolumSiraNo: number;
  pozisyonSn: number;
  oynatmaHizi: number;
  tamamlanmaYuzdesi: number;
  cihazKimligi: string;
  cihazAdi: string | null;
  istemciZamani: string;
  tamamlandiMi: boolean;
  guncellemeTarihi: string;
}

export interface ProgressUpdateRequest {
  chapterId: string;
  pozisyonSn: number;
  oynatmaHizi: number;
  cihazKimligi: string;
  cihazAdi?: string | null;
  istemciZamani: string;
  tamamlandiMi?: boolean;
}
