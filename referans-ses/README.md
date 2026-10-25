# referans-ses — ses klonlama için referans kaydınız

Bu klasörün içeriği **kişisel ses verinizdir; Git'e eklenmez** (`.gitignore`).

## Ne koymalısınız?

| Dosya | Açıklama |
|---|---|
| `ses.wav` (veya `.mp3`, `.m4a`, `.ogg`, `.flac`) | **10–20 saniyelik** temiz kendi ses kaydınız |
| `ses.txt` | Kayıtta söylediklerinizin **birebir yazısı** (noktalama dahil, tek paragraf) |

## İyi bir kayıt için

- Sessiz oda, yankısız, mikrofon ağzınıza yakın (telefon sesli not uygulaması yeterli).
- Doğal, sakin **kitap okur gibi** konuşun; fısıldamayın, bağırmayın.
- Müzik, arka plan sesi, nefes patlaması olmasın. Kayıt sırasında kısa duraksamalar sorun değil.
- Önerilen metin: ilgi çekici bir kitaptan 3–4 cümlelik düz bir anlatı paragrafı.

> Başkasının sesini yalnızca **izniyle** klonlayın (geliştirici dokümanı Bölüm 1.7 etik çerçeve).

## Sonra

```powershell
src\SesliKitap.PythonWorker\.venv\Scripts\python.exe scripts\ses-karsilastirma.py
```

Çıktılar `sonuc\` klasörüne yazılır (`temel_01.wav` … `trendyol_06.wav`).
