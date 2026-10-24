using FluentValidation;
using SesliKitap.Business.DTOs.Synthesis;

namespace SesliKitap.Business.Validators;

/// <summary>
/// <see cref="SynthesisStartRequest"/> için FluentValidation doğrulayıcısı (Bölüm 4.15.4).
/// </summary>
public sealed class SynthesisStartRequestValidator : AbstractValidator<SynthesisStartRequest>
{
    /// <summary>
    /// <see cref="SynthesisStartRequestValidator"/> kurallarını tanımlar.
    /// </summary>
    public SynthesisStartRequestValidator()
    {
        RuleFor(x => x.VoiceProfileId)
            .NotEmpty()
            .WithMessage("Ses profili seçilmelidir.");

        RuleFor(x => x.ChapterIds)
            .Must(ids => ids is null || ids.Count is > 0 and <= 2000)
            .WithMessage("Bölüm listesi boş olamaz ve 2000'i geçemez.");

        RuleForEach(x => x.ChapterIds)
            .NotEmpty()
            .WithMessage("Bölüm kimliği boş olamaz.");
    }
}
