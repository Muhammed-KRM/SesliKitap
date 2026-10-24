using FluentValidation;
using SesliKitap.Business.DTOs.Pronunciation;

namespace SesliKitap.Business.Validators;

/// <summary>
/// <see cref="PronunciationCreateRequest"/> doğrulayıcısı.
/// </summary>
public sealed class PronunciationCreateRequestValidator : AbstractValidator<PronunciationCreateRequest>
{
    /// <summary>
    /// Doğrulama kurallarını başlatır.
    /// </summary>
    public PronunciationCreateRequestValidator()
    {
        RuleFor(x => x.Kelime)
            .NotEmpty().WithMessage("Kelime alanı boş olamaz.")
            .MaximumLength(100).WithMessage("Kelime en fazla 100 karakter olabilir.");

        RuleFor(x => x.Okunus)
            .NotEmpty().WithMessage("Okunuş alanı boş olamaz.")
            .MaximumLength(200).WithMessage("Okunuş en fazla 200 karakter olabilir.");

        RuleFor(x => x.Aciklama)
            .MaximumLength(500).WithMessage("Açıklama en fazla 500 karakter olabilir.");
    }
}

/// <summary>
/// <see cref="PronunciationUpdateRequest"/> doğrulayıcısı.
/// </summary>
public sealed class PronunciationUpdateRequestValidator : AbstractValidator<PronunciationUpdateRequest>
{
    /// <summary>
    /// Doğrulama kurallarını başlatır.
    /// </summary>
    public PronunciationUpdateRequestValidator()
    {
        RuleFor(x => x.Kelime)
            .NotEmpty().WithMessage("Kelime alanı boş olamaz.")
            .MaximumLength(100).WithMessage("Kelime en fazla 100 karakter olabilir.");

        RuleFor(x => x.Okunus)
            .NotEmpty().WithMessage("Okunuş alanı boş olamaz.")
            .MaximumLength(200).WithMessage("Okunuş en fazla 200 karakter olabilir.");

        RuleFor(x => x.Aciklama)
            .MaximumLength(500).WithMessage("Açıklama en fazla 500 karakter olabilir.");
    }
}

/// <summary>
/// <see cref="PronunciationPreviewRequest"/> doğrulayıcısı.
/// </summary>
public sealed class PronunciationPreviewRequestValidator : AbstractValidator<PronunciationPreviewRequest>
{
    /// <summary>
    /// Doğrulama kurallarını başlatır.
    /// </summary>
    public PronunciationPreviewRequestValidator()
    {
        RuleFor(x => x.Metin)
            .NotEmpty().WithMessage("Önizlenecek metin boş olamaz.")
            .MaximumLength(10000).WithMessage("Önizleme metni en fazla 10.000 karakter olabilir.");
    }
}
