export const T = {
  app: {
    title: 'SesliKitap',
    subtitle: 'Kişisel Türkçe Sesli Kitap Stüdyosu'
  },
  nav: {
    library: 'Kütüphane',
    discover: 'Keşfet',
    voices: 'Sesler',
    jobs: 'İşler',
    downloads: 'İndirilenler',
    settings: 'Ayarlar'
  },
  access: {
    title: 'Erişim Anahtarı',
    subtitle: 'SesliKitap sunucunuza bağlanmak için yönetici erişim anahtarınızı girin.',
    placeholder: 'API erişim anahtarınızı girin',
    connect: 'Bağlan',
    verifying: 'Doğrulanıyor...',
    invalid: 'Girilen erişim anahtarı geçersiz veya sunucuya ulaşılamadı.',
    required: 'Lütfen erişim anahtarını girin.'
  },
  library: {
    title: 'Kütüphanem',
    continue: 'Dinlemeye devam et',
    continueEmpty: 'Henüz yarım kalan bir dinlemeniz yok.',
    empty: 'Henüz kitap yok. Bir PDF, EPUB veya TXT dosyası yükleyin.',
    upload: 'Kitap Yükle',
    searchPlaceholder: 'Başlık veya yazar ara...',
    allStatuses: 'Tüm Durumlar',
    sortNewest: 'En Yeni',
    sortRecentListen: 'Son Dinlenen',
    sortTitle: 'Başlığa Göre (A-Z)',
    dropHint: 'Kitap dosyasını (.pdf, .epub, .txt) buraya bırakarak yükleyin',
    chaptersReady: 'bölüm hazır'
  },
  upload: {
    title: 'Yeni Kitap Yükle',
    dropArea: 'PDF, EPUB veya TXT dosyasını sürükleyip bırakın ya da seçin',
    maxSizeHint: 'Maksimum dosya boyutu: 300 MB',
    titleLabel: 'Kitap Başlığı (İsteğe bağlı)',
    authorLabel: 'Yazar (İsteğe bağlı)',
    submit: 'Yükle ve İşle',
    uploading: 'Yükleniyor...',
    duplicateTitle: 'Bu kitap zaten kütüphanenizde mevcut',
    goToBook: 'Kitaba Git',
    invalidExtension: 'Yalnızca .pdf, .epub veya .txt uzantılı dosyalar yüklenebilir.',
    tooLarge: 'Dosya boyutu 300 MB sınırını aşıyor.'
  },
  status: {
    Indiriliyor: 'İndiriliyor',
    Isleniyor: 'İşleniyor',
    OnizlemeBekliyor: 'İnceleme bekliyor',
    Seslendiriliyor: 'Seslendiriliyor',
    KismenHazir: 'Kısmen hazır',
    Hazir: 'Hazır',
    OcrGerekli: 'OCR gerekli',
    Hata: 'Hata'
  },
  chapterAudioStatus: {
    Yok: 'Ses yok',
    Kuyrukta: 'Kuyrukta',
    Seslendiriliyor: 'Seslendiriliyor',
    Hazir: 'Hazır',
    GuncelDegil: 'Metin değişti',
    Hata: 'Hata'
  },
  jobStatus: {
    Kuyrukta: 'Kuyrukta',
    Calisiyor: 'Çalışıyor',
    Tamamlandi: 'Tamamlandı',
    KismiTamamlandi: 'Kısmen tamamlandı',
    IptalEdildi: 'İptal edildi',
    Hata: 'Hata'
  },
  voiceStatus: {
    Isleniyor: 'Hazırlanıyor',
    Hazir: 'Hazır',
    Hata: 'Hata'
  },
  bookDetail: {
    listen: 'Dinle',
    synthesize: 'Seslendir',
    downloadOffline: 'Çevrimdışı İndir',
    export: 'Dışa Aktar',
    reprocess: 'Yeniden İşle',
    deleteBook: 'Kitabı Sil',
    tabChapters: 'Bölümler',
    tabSynthesis: 'Seslendirme',
    tabInfo: 'Kitap Bilgisi',
    ocrWarning: 'Bu PDF taranmış görüntülerden oluşuyor. Seslendirme öncesi OCR uygulanmalıdır.',
    runOcr: 'OCR ile Metne Çevir',
    mergeSelected: 'Seçilileri Birleştir',
    excludeSelected: 'Seçilileri Hariç Tut',
    includeSelected: 'Seçilileri Dahil Et',
    synthesizeSelected: 'Seçilileri Seslendir',
    editChapter: 'Bölümü Düzenle',
    splitAtCursor: 'İmleçten Böl',
    saveChapter: 'Değişiklikleri Kaydet',
    conflictWarning: 'Bu bölüm başka bir sekmede değiştirildi. Lütfen sayfayı yenileyin.'
  },
  synthesis: {
    title: 'Seslendirme Ayarları ve Maliyet Tahmini',
    selectVoice: 'Ses Profili Seçin',
    scopeAll: 'Tüm dahil bölümler',
    scopeMissing: 'Yalnızca eksik veya metni değişmiş bölümler',
    scopeSelected: 'Bölüm listesinden seçilenler',
    estimateTitle: 'Tahmini Süre ve Maliyet',
    estimatedAudio: 'Tahmini Ses Süresi',
    estimatedGpu: 'Tahmini GPU Süresi',
    estimatedCost: 'Tahmini Maliyet',
    monthlyBudget: 'Aylık Bütçe Durumu',
    budgetExceeded: 'Aylık bütçe sınırı aşılıyor. Yine de başlatmak için onay kutusunu işaretleyin.',
    forceApprove: 'Bütçe aşımını onaylıyorum, yine de başlat',
    startButton: 'Seslendirmeyi Başlat',
    cancelButton: 'İşi İptal Et',
    retryButton: 'Eksikleri Yeniden Dene'
  },
  voices: {
    title: 'Ses Profilleri',
    createNew: 'Yeni Ses Profili',
    defaultBadge: 'Varsayılan',
    makeDefault: 'Varsayılan Yap',
    listenSample: 'Örnek Dinle',
    uploadTab: 'Dosya Yükle',
    recordTab: 'Mikrofonla Kaydet',
    nameLabel: 'Profil Adı',
    descriptionLabel: 'Açıklama',
    genderLabel: 'Cinsiyet',
    promptTextLabel: 'Referans Metni (Kayıtta okunan metin)',
    suggestedPrompt:
      'Güneşli bir sonbahar sabahında, boğazın serin rüzgârı vapurun güvertesinde oturan yolcuların yüzünü okşuyordu. Uzakta martılar suyun üzerinde süzülüyor, şehrin kadim kuleleri sislerin arasından yavaşça beliriyordu.',
    useSuggestedPrompt: 'Önerilen Metni Kullan',
    consentLabel: 'Bu sesi kullanma hakkım var; başkasına ait sesi izinsiz kullanmayacağım.',
    tips: 'Sessiz ortam, 10–20 sn, doğal ve sakin okuma, mikrofona 20 cm mesafe önerilir.'
  },
  jobs: {
    title: 'Sentez İşleri',
    empty: 'Henüz başlatılmış bir seslendirme işi bulunmuyor.'
  },
  player: {
    play: 'Oynat',
    pause: 'Duraklat',
    skipBackward: 'Geri Sar',
    skipForward: 'İleri Sar',
    prevChapter: 'Önceki Bölüm',
    nextChapter: 'Sonraki Bölüm',
    speed: 'Oynatma Hızı',
    sleepTimer: 'Uyku Zamanlayıcısı',
    sleepEndOfChapter: 'Bölüm Sonunda Durdur',
    sleepOff: 'Zamanlayıcıyı Kapat',
    chapters: 'Bölümler',
    bookmarks: 'Yer İmleri',
    addBookmark: 'Yer İmi Ekle',
    showText: 'Metni Göster',
    hideText: 'Metni Gizle',
    returnToReading: 'Okunan Yere Dön'
  },
  downloads: {
    title: 'Çevrimdışı İndirilenler',
    empty: 'Henüz çevrimdışı dinlemek için indirilmiş kitap bulunmuyor.',
    storageUsed: 'Kullanılan Depolama',
    deleteAll: 'Tümünü Sil',
    updateAvailable: 'Güncelleme var'
  },
  discover: {
    title: 'Kitap Keşfet',
    searchPlaceholder: 'Kitap adı veya yazar ara (örn. Ömer Seyfettin, Sabahattin Ali)...',
    searchButton: 'Ara',
    empty: 'Arama yapmak için yukarıdaki kutuya kitap veya yazar adı yazın.',
    noResults: 'Aramanızla eşleşen indirilebilir Türkçe kitap bulunamadı.',
    inLibrary: 'Kütüphanede Var',
    addToLibrary: 'Kütüphaneme Ekle',
    viewFiles: 'Dosyaları Gör',
    webCopyrightWarning:
      'Web araması doğrulanmamış dış kaynakları listeler. Yalnızca telif hakkı süresi dolmuş veya kullanım hakkınız olan eserleri içe aktarın.'
  },
  settings: {
    title: 'Ayarlar',
    theme: 'Görünüm Teması',
    dark: 'Karanlık',
    light: 'Açık',
    skipBackward: 'Geri Sarma Süresi (sn)',
    skipForward: 'İleri Sarma Süresi (sn)',
    defaultSpeed: 'Varsayılan Oynatma Hızı',
    audioFormat: 'Tercih Edilen Ses Formatı',
    formatAuto: 'Otomatik (Önerilen - Opus / MP3)',
    formatOpus: 'Her Zaman Opus (Düşük Boyut)',
    formatMp3: 'Her Zaman MP3 (Geniş Uyumluluk)',
    deviceName: 'Bu Cihazın Adı',
    logout: 'Erişim Anahtarını Temizle ve Çıkış Yap',
    healthTitle: 'Sistem ve Altyapı Sağlığı'
  },
  common: {
    cancel: 'Vazgeç',
    confirm: 'Onayla',
    save: 'Kaydet',
    delete: 'Sil',
    close: 'Kapat',
    loading: 'Yükleniyor...',
    offlineWarning: 'Çevrimdışısınız. Yalnızca indirilen kitaplar dinlenebilir.'
  }
} as const;
