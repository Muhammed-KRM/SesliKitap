namespace SesliKitap.Data.Enums;

/// <summary>
/// Kitabın kütüphaneye eklendiği kaynak türü.
/// </summary>
public enum KaynakTipi
{
    /// <summary>Kullanıcı tarafından doğrudan dosya yüklemesi.</summary>
    Yukleme = 0,

    /// <summary>Internet Archive açık arşivinden içe aktarma.</summary>
    InternetArchive = 1,

    /// <summary>Open Library kataloğundan içe aktarma.</summary>
    OpenLibrary = 2,

    /// <summary>Project Gutenberg (Gutendex) arşivinden içe aktarma.</summary>
    Gutenberg = 3,

    /// <summary>Genel web aramasından içe aktarma.</summary>
    Web = 4
}
