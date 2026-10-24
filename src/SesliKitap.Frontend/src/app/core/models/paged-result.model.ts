export interface PagedResult<T> {
  kayitlar: T[];
  toplamKayit: number;
  sayfa: number;
  boyut: number;
  toplamSayfa: number;
}
