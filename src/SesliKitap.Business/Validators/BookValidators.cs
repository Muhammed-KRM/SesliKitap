using FluentValidation;
using Microsoft.Extensions.Options;
using SesliKitap.Business.DTOs.Book;
using SesliKitap.Business.Options;

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
