using FluentValidation;
using SesliKitap.Business.DTOs.Progress;

namespace SesliKitap.Business.Validators;

/// <summary>
/// <see cref="ProgressUpdateRequest"/> için FluentValidation doğrulayıcısı.
/// </summary>
public sealed class ProgressUpdateRequestValidator : AbstractValidator<ProgressUpdateRequest>
{
    /// <summary>
    /// <see cref="ProgressUpdateRequestValidator"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ProgressUpdateRequestValidator()
    {
        RuleFor(x => x.ChapterId)
            .NotEmpty()
            .WithMessage("Bölüm kimliği zorunludur.");

        RuleFor(x => x.PozisyonSn)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Dinleme konumu negatif olamaz.");

        RuleFor(x => x.OynatmaHizi)
            .InclusiveBetween(0.5, 3.0)
            .WithMessage("Oynatma hızı 0.5x ile 3.0x arasında olmalıdır.");

        RuleFor(x => x.CihazKimligi)
            .NotEmpty()
            .WithMessage("Cihaz kimliği zorunludur.")
            .MaximumLength(64)
            .WithMessage("Cihaz kimliği en fazla 64 karakter olabilir.");

        RuleFor(x => x.CihazAdi)
            .MaximumLength(100)
            .WithMessage("Cihaz adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.IstemciZamani)
            .NotEmpty()
            .WithMessage("İstemci zaman damgası zorunludur.");
    }
}
