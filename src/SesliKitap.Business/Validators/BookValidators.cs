using System.Net;
using FluentValidation;
using Microsoft.Extensions.Options;
using SesliKitap.Business.DTOs.Book;
using SesliKitap.Business.Options;
using SesliKitap.Data.Enums;

namespace SesliKitap.Business.Validators;

/// <summary>
/// <see cref="BookUploadRequest"/> doğrulama kuralları.
/// </summary>
public sealed class BookUploadRequestValidator : AbstractValidator<BookUploadRequest>
{
    /// <summary>
    /// <see cref="BookUploadRequestValidator"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public BookUploadRequestValidator(IOptions<UploadOptions> uploadOptions)
    {
        var opts = uploadOptions.Value;

        RuleFor(x => x.Dosya)
            .NotNull()
            .WithMessage("Yüklenecek kitap dosyası seçilmelidir.")
            .DependentRules(() =>
            {
                RuleFor(x => x.Dosya.Length)
                    .GreaterThan(0)
                    .WithMessage("Yüklenen dosya boş olamaz.")
                    .LessThanOrEqualTo(opts.MaxBookBytes)
                    .WithMessage($"Dosya boyutu en fazla {opts.MaxBookBytes / (1024 * 1024)} MB olabilir.");

                RuleFor(x => x.Dosya.FileName)
                    .NotEmpty()
                    .WithMessage("Dosya adı boş olamaz.")
                    .Must(fileName =>
                    {
                        var ext = Path.GetExtension(fileName);
                        return !string.IsNullOrEmpty(ext) &&
                               opts.AllowedBookExtensions.Any(a => string.Equals(a, ext, StringComparison.OrdinalIgnoreCase));
                    })
                    .WithMessage($"Yalnızca şu dosya uzantıları desteklenir: {string.Join(", ", opts.AllowedBookExtensions)}");
            });

        RuleFor(x => x.Baslik)
            .MaximumLength(500)
            .WithMessage("Kitap başlığı en fazla 500 karakter olabilir.");

        RuleFor(x => x.Yazar)
            .MaximumLength(300)
            .WithMessage("Yazar adı en fazla 300 karakter olabilir.");
    }
}

/// <summary>
/// <see cref="BookUpdateRequest"/> doğrulama kuralları.
/// </summary>
public sealed class BookUpdateRequestValidator : AbstractValidator<BookUpdateRequest>
{
    /// <summary>
    /// <see cref="BookUpdateRequestValidator"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public BookUpdateRequestValidator()
    {
        RuleFor(x => x.Baslik)
            .NotEmpty()
            .WithMessage("Kitap başlığı boş olamaz.")
            .MaximumLength(500)
            .WithMessage("Kitap başlığı en fazla 500 karakter olabilir.");

        RuleFor(x => x.Yazar)
            .MaximumLength(300)
            .WithMessage("Yazar adı en fazla 300 karakter olabilir.");

        RuleFor(x => x.Aciklama)
            .MaximumLength(10000)
            .WithMessage("Açıklama en fazla 10.000 karakter olabilir.");
    }
}

/// <summary>
/// <see cref="BookImportRequest"/> doğrulama kuralları (URL şeması, izinli alan adları ve SSRF ön kontrolü).
/// </summary>
public sealed class BookImportRequestValidator : AbstractValidator<BookImportRequest>
{
    private static readonly string[] AllowedTrustedDomains =
    [
        "archive.org",
        "gutenberg.org",
        "openlibrary.org"
    ];

    /// <summary>
    /// <see cref="BookImportRequestValidator"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public BookImportRequestValidator(IOptions<DiscoveryOptions> discoveryOptions)
    {
        var webEnabled = discoveryOptions.Value.WebSearch.Enabled;

        RuleFor(x => x.KaynakTipi)
            .IsInEnum()
            .NotEqual(KaynakTipi.Yukleme)
            .WithMessage("İçe aktarma için geçerli bir dış kaynak tipi seçilmelidir.");

        RuleFor(x => x.KaynakKimligi)
            .NotEmpty()
            .WithMessage("Kaynak kimliği boş olamaz.")
            .MaximumLength(200)
            .WithMessage("Kaynak kimliği en fazla 200 karakter olabilir.");

        RuleFor(x => x.Baslik)
            .NotEmpty()
            .WithMessage("Kitap başlığı boş olamaz.")
            .MaximumLength(500)
            .WithMessage("Kitap başlığı en fazla 500 karakter olabilir.");

        RuleFor(x => x.Yazar)
            .MaximumLength(300)
            .WithMessage("Yazar adı en fazla 300 karakter olabilir.");

        RuleFor(x => x.DosyaFormati)
            .IsInEnum()
            .WithMessage("Geçersiz dosya formatı.");

        RuleFor(x => x.IndirmeUrl)
            .NotEmpty()
            .WithMessage("İndirme adresi (URL) boş olamaz.")
            .MaximumLength(2000)
            .WithMessage("İndirme adresi en fazla 2000 karakter olabilir.")
            .Must(url => IsSafeAndAllowedUrl(url, webEnabled))
            .WithMessage("Güvenli olmayan veya izin verilmeyen indirme adresi.");
    }

    private static bool IsSafeAndAllowedUrl(string? rawUrl, bool webSearchEnabled)
    {
        if (string.IsNullOrWhiteSpace(rawUrl) || !Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.Host.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(host) ||
            host is "localhost" or "minio" or "postgres" or "redis" or "rabbitmq" or "searxng" or "api" ||
            host.EndsWith(".local", StringComparison.Ordinal) ||
            host.EndsWith(".internal", StringComparison.Ordinal))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            if (IPAddress.IsLoopback(ip) || IsPrivateOrReservedIp(ip))
            {
                return false;
            }
        }

        if (webSearchEnabled)
        {
            return true;
        }

        foreach (var domain in AllowedTrustedDomains)
        {
            if (host == domain || host.EndsWith("." + domain, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPrivateOrReservedIp(IPAddress ip)
    {
        if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
        {
            return true;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 4)
        {
            // 0.0.0.0/8, 10.0.0.0/8, 100.64.0.0/10, 127.0.0.0/8, 169.254.0.0/16, 172.16.0.0/12, 192.168.0.0/16
            if (bytes[0] is 0 or 10 or 127) return true;
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            if (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) return true;
        }

        return false;
    }
}
