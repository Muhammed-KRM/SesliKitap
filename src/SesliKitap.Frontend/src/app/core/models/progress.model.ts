export interface ListeningProgressDto {
  bookId: string;
  chapterId: string;
  bolumSiraNo: number;
  konumSn: number;
  cumleIndeksi: number | null;
  oynatmaHizi: number;
  tamamlanmaYuzdesi: number;
  sonCihazKimligi: string;
  sonCihazAdi: string | null;
  istemciZamanDamgasi: string;
  guncellemeTarihi: string;
}

export interface ProgressUpdateRequest {
  chapterId: string;
  konumSn: number;
  cumleIndeksi?: number | null;
  oynatmaHizi: number;
  cihazKimligi: string;
  cihazAdi?: string | null;
  istemciZamanDamgasi: string;
}
