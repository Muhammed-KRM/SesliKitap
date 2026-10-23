namespace SesliKitap.Data.Enums;

/// <summary>
/// Desteklenen metinden sese (TTS) yapay zeka motor aileleri.
/// </summary>
public enum TtsMotoru
{
    /// <summary>OpenBMB VoxCPM2 (2B parametre, 48 kHz ses klonlama).</summary>
    VoxCpm2 = 0,

    /// <summary>Coqui XTTS-v2 (çok dilli ses klonlama yedeği).</summary>
    Xtts2 = 1,

    /// <summary>Otomatik testler için deterministik yerel sahte motor.</summary>
    Mock = 99
}
