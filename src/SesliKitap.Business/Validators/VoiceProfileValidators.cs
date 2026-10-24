using FluentValidation;
using Microsoft.Extensions.Options;
using SesliKitap.Business.DTOs.Voice;
using SesliKitap.Business.Options;

namespace SesliKitap.Business.Validators;

/// <summary>
/// <see cref="VoiceProfileCreateRequest"/> için FluentValidation doğrulayıcısı.
/// </summary>
public sealed class VoiceProfileCreateRequestValidator : AbstractValidator<VoiceProfileCreateRequest>
{
    /// <summary>
    /// <see cref="VoiceProfileCreateRequestValidator"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public VoiceProfileCreateRequestValidator(IOptions<UploadOptions> uploadOptions)
    {
        var opts = uploadOptions.Value;

        RuleFor(x => x.Ad)
            .NotEmpty().WithMessage("Ses profili adı boş olamaz.")
            .MaximumLength(100).WithMessage("Ses profili adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.Aciklama)
            .MaximumLength(500).WithMessage("Açıklama en fazla 500 karakter olabilir.");

        RuleFor(x => x.ReferansMetin)
            .MaximumLength(2000).WithMessage("Referans metni en fazla 2000 karakter olabilir.");

        RuleFor(x => x.KullanimOnayi)
            .Equal(true).WithMessage("Ses klonlama işlemi için etik kullanım onayını (kullanimOnayi=true) kabul etmelisiniz.");

        RuleFor(x => x.Ses)
            .NotNull().WithMessage("Referans ses dosyası zorunludur.")
            .Must(f => f is not null && f.Length > 0).WithMessage("Boş ses dosyası yüklenemez.")
            .Must(f => f is null || f.Length <= opts.MaxVoiceBytes)
            .WithMessage($"Ses dosyası boyutu en fazla {opts.MaxVoiceBytes / (1024 * 1024)} MB olabilir.")
            .Must(f =>
            {
                if (f is null)
                {
                    return false;
                }

                var ext = Path.GetExtension(f.FileName)?.ToLowerInvariant() ?? string.Empty;
                return opts.AllowedVoiceExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
            })
            .WithMessage($"Desteklenmeyen ses uzantısı. İzin verilenler: {string.Join(", ", opts.AllowedVoiceExtensions)}");
    }
}

/// <summary>
/// <see cref="VoiceProfileUpdateRequest"/> için FluentValidation doğrulayıcısı.
/// </summary>
public sealed class VoiceProfileUpdateRequestValidator : AbstractValidator<VoiceProfileUpdateRequest>
{
    /// <summary>
    /// <see cref="VoiceProfileUpdateRequestValidator"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public VoiceProfileUpdateRequestValidator()
    {
        RuleFor(x => x.Ad)
            .NotEmpty().WithMessage("Ses profili adı boş olamaz.")
            .MaximumLength(100).WithMessage("Ses profili adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.Aciklama)
            .MaximumLength(500).WithMessage("Açıklama en fazla 500 karakter olabilir.");

        RuleFor(x => x.ReferansMetin)
            .MaximumLength(2000).WithMessage("Referans metni en fazla 2000 karakter olabilir.");

        When(x => x.Parametreler is not null, () =>
        {
            RuleFor(x => x.Parametreler!.CfgValue)
                .InclusiveBetween(0.5, 5.0).WithMessage("cfgValue 0.5 ile 5.0 arasında olmalıdır.");

            RuleFor(x => x.Parametreler!.InferenceTimesteps)
                .InclusiveBetween(4, 30).WithMessage("inferenceTimesteps 4 ile 30 arasında olmalıdır.");

            RuleFor(x => x.Parametreler!.KonusmaHizi)
                .InclusiveBetween(0.5, 2.0).WithMessage("konusmaHizi 0.5 ile 2.0 arasında olmalıdır.");
        });
    }
}
