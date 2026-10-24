"""TurkishNormalizer kapsamlı birim testleri (>= 150 parametrize örnek, Bölüm 4.15.8)."""

from __future__ import annotations

import pytest

from pipeline.text.pronunciation_applier import PronunciationEntry
from pipeline.text.turkish_normalizer import TurkishNormalizer

_NORMALIZER = TurkishNormalizer()

# 1. Tam sayılar ve yıllar (25 örnek)
INTEGER_CASES = [
    ("0", "sıfır."),
    ("1", "bir."),
    ("2", "iki."),
    ("3", "üç."),
    ("4", "dört."),
    ("5", "beş."),
    ("6", "altı."),
    ("7", "yedi."),
    ("8", "sekiz."),
    ("9", "dokuz."),
    ("10", "on."),
    ("11", "on bir."),
    ("19", "on dokuz."),
    ("20", "yirmi."),
    ("35", "otuz beş."),
    ("42", "kırk iki."),
    ("99", "doksan dokuz."),
    ("100", "yüz."),
    ("101", "yüz bir."),
    ("250", "iki yüz elli."),
    ("1000", "bin."),
    ("1001", "bin bir."),
    ("1453", "bin dört yüz elli üç."),
    ("1923", "bin dokuz yüz yirmi üç."),
    ("2026", "iki bin yirmi altı."),
]

# 2. Büyük sayılar ve binlik ayraçlı sayılar (15 örnek)
THOUSANDS_AND_LARGE_CASES = [
    ("1.000", "bin."),
    ("2.500", "iki bin beş yüz."),
    ("10.000", "on bin."),
    ("50.000", "elli bin."),
    ("100.000", "yüz bin."),
    ("250.000", "iki yüz elli bin."),
    ("1.000.000", "bir milyon."),
    ("1.250.000", "bir milyon iki yüz elli bin."),
    ("85.000.000", "seksen beş milyon."),
    ("1.000.000.000", "bir milyar."),
    ("2.300.400", "iki milyon üç yüz bin dört yüz."),
    ("12.345", "on iki bin üç yüz kırk beş."),
    ("999.999", "dokuz yüz doksan dokuz bin dokuz yüz doksan dokuz."),
    ("-5", "eksi beş."),
    ("-40", "eksi kırk."),
]

# 3. Ondalık sayılar (12 örnek)
DECIMAL_CASES = [
    ("3,14", "üç virgül on dört."),
    ("0,5", "sıfır virgül beş."),
    ("0,05", "sıfır virgül sıfır beş."),
    ("0,007", "sıfır virgül sıfır sıfır yedi."),
    ("12,5", "on iki virgül beş."),
    ("99,9", "doksan dokuz virgül dokuz."),
    ("1,25", "bir virgül yirmi beş."),
    ("2,718", "iki virgül yedi yüz on sekiz."),
    ("10,01", "on virgül sıfır bir."),
    ("-2,5", "eksi iki virgül beş."),
    ("3.14", "üç virgül on dört."),
    ("0.75", "sıfır virgül yetmiş beş."),
]

# 4. Sıra sayıları ve "cümle sonu yıl" tuzağı (18 örnek)
ORDINAL_CASES = [
    ("1. bölüm", "birinci bölüm."),
    ("2. bölüm", "ikinci bölüm."),
    ("3. bölüm", "üçüncü bölüm."),
    ("4. madde", "dördüncü madde."),
    ("5. kat", "beşinci kat."),
    ("6. his", "altıncı his."),
    ("7. gün", "yedinci gün."),
    ("8. sınıf", "sekizinci sınıf."),
    ("9. senfoni", "dokuzuncu senfoni."),
    ("10. yıl", "onuncu yıl."),
    ("20. yüzyıl", "yirminci yüzyıl."),
    ("21. yüzyıl", "yirmi birinci yüzyıl."),
    ("100. yıl", "yüzüncü yıl."),
    ("1'inci sırada", "birinci sırada."),
    ("2'nci sırada", "ikinci sırada."),
    ("3'üncü sırada", "üçüncü sırada."),
    ("4'üncü sırada", "dördüncü sırada."),
    ("Cumhuriyetin kuruluş yılı 1923.", "Cumhuriyetin kuruluş yılı bin dokuz yüz yirmi üç."),
]

# 5. Romen rakamları ve bağlam duyarlılığı (12 örnek)
ROMAN_CASES = [
    ("IV. Murad", "dördüncü Murad."),
    ("II. Abdülhamid", "ikinci Abdülhamid."),
    ("II. Mehmed", "ikinci Mehmed."),
    ("I. Süleyman", "birinci Süleyman."),
    ("III. Selim", "üçüncü Selim."),
    ("XVI. Louis", "on altıncı Louis."),
    ("XIX. yüzyıl", "on dokuzuncu yüzyıl."),
    ("XVIII. yüzyıl", "on sekizinci yüzyıl."),
    ("XX. asır", "yirminci asır."),
    ("III", "üç."),
    ("VII.", "yedi."),
    ("XII", "on iki."),
]

# 6. Tarihler ve kesme işaretli sayı ekleri (15 örnek)
DATE_AND_SUFFIX_CASES = [
    ("29 Ekim 1923", "yirmi dokuz Ekim bin dokuz yüz yirmi üç."),
    ("29.10.1923", "yirmi dokuz Ekim bin dokuz yüz yirmi üç."),
    ("19/05/1919", "on dokuz Mayıs bin dokuz yüz on dokuz."),
    ("23 Nisan 1920", "yirmi üç Nisan bin dokuz yüz yirmi."),
    ("30 Ağustos 1922", "otuz Ağustos bin dokuz yüz yirmi iki."),
    ("10 Kasım 1938", "on Kasım bin dokuz yüz otuz sekiz."),
    ("01.01.2000", "bir Ocak iki bin."),
    ("15 Temmuz", "on beş Temmuz."),
    ("1923'te", "bin dokuz yüz yirmi üçte."),
    ("1938'de", "bin dokuz yüz otuz sekizde."),
    ("2000'den", "iki binden."),
    ("1980'lerde", "bin dokuz yüz seksenlerde."),
    ("50'ye", "elliye."),
    ("100'üncü", "yüzüncü."),
    ("1914-1918", "bin dokuz yüz on dört bin dokuz yüz on sekiz."),
]

# 7. Saatler (10 örnek)
TIME_CASES = [
    ("14:30", "on dört otuz."),
    ("09:05", "dokuz sıfır beş."),
    ("00:00", "sıfır sıfır sıfır."),
    ("12:00", "on iki sıfır sıfır."),
    ("23:59", "yirmi üç elli dokuz."),
    ("08:15", "sekiz on beş."),
    ("saat 14.30", "saat on dört otuz."),
    ("Saat 09.05", "Saat dokuz sıfır beş."),
    ("saat 17.00", "saat on yedi sıfır sıfır."),
    ("saat 14.30'da", "saat on dört otuzda."),
]

# 8. Para birimleri (12 örnek)
CURRENCY_CASES = [
    ("250 TL", "iki yüz elli lira."),
    ("₺250", "iki yüz elli lira."),
    ("12,50 TL", "on iki lira elli kuruş."),
    ("₺99,90", "doksan dokuz lira doksan kuruş."),
    ("1.500 TL", "bin beş yüz lira."),
    ("$5", "beş dolar."),
    ("5 USD", "beş dolar."),
    ("$12,25", "on iki dolar yirmi beş sent."),
    ("5 €", "beş avro."),
    ("€100", "yüz avro."),
    ("10 EUR", "on avro."),
    ("£20", "yirmi sterlin."),
]

# 9. Yüzdeler ve semboller (12 örnek)
PERCENT_AND_SYMBOL_CASES = [
    ("%25", "yüzde yirmi beş."),
    ("%100", "yüzde yüz."),
    ("%0", "yüzde sıfır."),
    ("%12,5", "yüzde on iki virgül beş."),
    ("25%", "yüzde yirmi beş."),
    ("20 °C", "yirmi santigrat derece."),
    ("Ali & Veli", "Ali ve Veli."),
    ("§ 5", "paragraf beş."),
    ("№ 7", "numara yedi."),
    ("3 × 4", "üç çarpı dört."),
    ("2 + 2 = 4", "iki artı iki eşittir dört."),
    ("%0,5", "yüzde sıfır virgül beş."),
]

# 10. Kısaltmalar ve birimler (22 örnek)
ABBREVIATION_CASES = [
    ("Dr. Ahmet", "Doktor Ahmet."),
    ("Prof. Dr. Mehmet", "Profesör Doktor Mehmet."),
    ("Doç. Dr. Ayşe", "Doçent Doktor Ayşe."),
    ("Av. Kemal", "Avukat Kemal."),
    ("Sn. Başkan", "Sayın Başkan."),
    ("Hz. Muhammed", "Hazreti Muhammed."),
    ("elma, armut vb.", "elma, armut ve benzeri."),
    ("kitap, defter vs.", "kitap, defter vesaire."),
    ("Ahmet, Mehmet vd.", "Ahmet, Mehmet ve diğerleri."),
    ("bkz. s. 15", "bakınız sayfa on beş."),
    ("c. 2", "cilt iki."),
    ("M.Ö. 500", "milattan önce beş yüz."),
    ("M.S. 300", "milattan sonra üç yüz."),
    ("T.C. vatandaşları", "Türkiye Cumhuriyeti vatandaşları."),
    ("TBMM açıldı", "te be me me açıldı."),
    ("ABD başkanı", "a be de başkanı."),
    ("THY uçağı", "te ha ye uçağı."),
    ("NATO zirvesi", "nato zirvesi."),
    ("ASELSAN mühendisleri", "aselsan mühendisleri."),
    ("TÜBİTAK projesi", "tübitak projesi."),
    ("15 km yol", "on beş kilometre yol."),
    ("500 gr un", "beş yüz gram un."),
]

# 11. Yabancı isim sözlüğü, büyük harf, noktalama, URL/telefon (15 örnek)
LEXICON_AND_MISC_CASES = [
    ("Shakespeare eserleri", "Şekspir eserleri."),
    ("Nietzsche felsefesi", "Niçe felsefesi."),
    ("Goethe ve Schopenhauer", "Göte ve Şopenhauer."),
    ("Montaigne denemeleri", "Monteny denemeleri."),
    ("Descartes düşüncesi", "Dekart düşüncesi."),
    ("Rousseau ve Voltaire", "Ruso ve Volter."),
    ("Molière oyunları", "Molyer oyunları."),
    ("Einstein teorisi", "Aynştayn teorisi."),
    ("Mozart ve Beethoven", "Motsart ve Betovın."),
    ("SESSİZCE yaklaştı", "sessizce yaklaştı."),
    ("— Merhaba! Nasılsın???", "Merhaba! Nasılsın?"),
    ("Detaylar https://ornek.com adresinde", "Detaylar adresinde."),
    ("İletişim info@seslikitap.com adresidir", "İletişim adresidir."),
    ("Raif Efendi (sessizce) konuştu", "Raif Efendi, sessizce, konuştu."),
    ("9789750802942", "dokuz yedi sekiz dokuz yedi beş sıfır sekiz sıfır iki dokuz dört iki."),
]

ALL_PARAMETERIZED_CASES = (
    INTEGER_CASES
    + THOUSANDS_AND_LARGE_CASES
    + DECIMAL_CASES
    + ORDINAL_CASES
    + ROMAN_CASES
    + DATE_AND_SUFFIX_CASES
    + TIME_CASES
    + CURRENCY_CASES
    + PERCENT_AND_SYMBOL_CASES
    + ABBREVIATION_CASES
    + LEXICON_AND_MISC_CASES
)


def test_total_parameterized_examples_at_least_150() -> None:
    assert len(ALL_PARAMETERIZED_CASES) >= 150


@pytest.mark.parametrize(("raw", "expected"), ALL_PARAMETERIZED_CASES)
def test_turkish_normalizer_rules(raw: str, expected: str) -> None:
    assert _NORMALIZER.normalize(raw) == expected


def test_user_pronunciation_overrides_default_lexicon() -> None:
    custom = TurkishNormalizer(
        lexicon=[PronunciationEntry(word="Shakespeare", spoken="Şeykspir", case_sensitive=False)]
    )
    assert custom.normalize("Shakespeare soneleri") == "Şeykspir soneleri."
