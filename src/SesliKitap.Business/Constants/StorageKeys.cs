namespace SesliKitap.Business.Constants;

/// <summary>
/// MinIO nesne depolama anahtarlarını (object keys) üreten saf yardımcı sınıf (Bölüm 4.12).
/// </summary>
public static class StorageKeys
{
    /// <summary>
    /// Kitabın orijinal kaynak dosyası anahtarı: <c>books/{bookId}/source/original.{ext}</c>.
    /// </summary>
    public static string BookSource(Guid bookId, string ext)
    {
        var cleanExt = ext.TrimStart('.').ToLowerInvariant();
        return $"books/{bookId:D}/source/original.{cleanExt}";
    }

    /// <summary>
    /// Kitap kapağı anahtarı: <c>books/{bookId}/cover.jpg</c>.
    /// </summary>
    public static string BookCover(Guid bookId) => $"books/{bookId:D}/cover.jpg";

    /// <summary>
    /// Worker tarafından çıkarılan bölümler JSON anahtarı: <c>books/{bookId}/processed/chapters.json</c>.
    /// </summary>
    public static string BookChaptersJson(Guid bookId) => $"books/{bookId:D}/processed/chapters.json";

    /// <summary>
    /// Worker tarafından üretilen metin çıkarma ve temizlik raporu JSON anahtarı: <c>books/{bookId}/processed/extraction-report.json</c>.
    /// </summary>
    public static string BookExtractionReportJson(Guid bookId) => $"books/{bookId:D}/processed/extraction-report.json";

    /// <summary>
    /// Bölüm ses dosyası anahtarı: <c>books/{bookId}/audio/{chapterId}/v{version}/chapter.{ext}</c>.
    /// </summary>
    public static string ChapterAudio(Guid bookId, Guid chapterId, int version, string ext)
    {
        var cleanExt = ext.TrimStart('.').ToLowerInvariant();
        return $"books/{bookId:D}/audio/{chapterId:D}/v{version}/chapter.{cleanExt}";
    }

    /// <summary>
    /// Bölüm cümle zaman damgaları JSON anahtarı: <c>books/{bookId}/audio/{chapterId}/v{version}/timestamps.json</c>.
    /// </summary>
    public static string ChapterTimestamps(Guid bookId, Guid chapterId, int version) =>
        $"books/{bookId:D}/audio/{chapterId:D}/v{version}/timestamps.json";

    /// <summary>
    /// Kitaba ait tüm ses dosyalarının bulunduğu önek: <c>books/{bookId}/audio/</c>.
    /// </summary>
    public static string BookAudioPrefix(Guid bookId) => $"books/{bookId:D}/audio/";

    /// <summary>
    /// Kitaba ait tüm nesnelerin bulunduğu kök önek: <c>books/{bookId}/</c>.
    /// </summary>
    public static string BookPrefix(Guid bookId) => $"books/{bookId:D}/";

    /// <summary>
    /// Ses profili ham ses dosyası anahtarı: <c>voices/{voiceProfileId}/raw.{ext}</c>.
    /// </summary>
    public static string VoiceRaw(Guid voiceProfileId, string ext)
    {
        var cleanExt = ext.TrimStart('.').ToLowerInvariant();
        return $"voices/{voiceProfileId:D}/raw.{cleanExt}";
    }

    /// <summary>
    /// Ses profili hazırlanmış 24 kHz mono WAV referans anahtarı: <c>voices/{voiceProfileId}/reference.wav</c>.
    /// </summary>
    public static string VoiceReference(Guid voiceProfileId) => $"voices/{voiceProfileId:D}/reference.wav";

    /// <summary>
    /// Ses profili örnek ses anahtarı: <c>voices/{voiceProfileId}/sample.ogg</c>.
    /// </summary>
    public static string VoiceSample(Guid voiceProfileId) => $"voices/{voiceProfileId:D}/sample.ogg";

    /// <summary>
    /// Ses profiline ait tüm nesnelerin bulunduğu kök önek: <c>voices/{voiceProfileId}/</c>.
    /// </summary>
    public static string VoicePrefix(Guid voiceProfileId) => $"voices/{voiceProfileId:D}/";

    /// <summary>
    /// Sentez işi girdi JSON anahtarı: <c>synthesis/{jobId}/input.json</c>.
    /// </summary>
    public static string SynthesisInput(Guid jobId) => $"synthesis/{jobId:D}/input.json";

    /// <summary>
    /// Kitap M4B dışa aktarım dosyası anahtarı: <c>books/{bookId}/export/{slug}.m4b</c> (Faz 8).
    /// </summary>
    public static string BookExportM4b(Guid bookId, string slug) => $"books/{bookId:D}/export/{slug}.m4b";

    /// <summary>
    /// Kitap dışa aktarım klasörü öneki: <c>books/{bookId}/export/</c> (Faz 8).
    /// </summary>
    public static string BookExportPrefix(Guid bookId) => $"books/{bookId:D}/export/";
}
