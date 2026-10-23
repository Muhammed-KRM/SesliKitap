using FluentValidation;
using SesliKitap.Business.DTOs.Chapter;

namespace SesliKitap.Business.Validators;

/// <summary>
/// <see cref="ChapterUpdateRequest"/> doğrulama kuralları.
/// </summary>
public sealed class ChapterUpdateRequestValidator : AbstractValidator<ChapterUpdateRequest>
{
    /// <summary>
    /// <see cref="ChapterUpdateRequestValidator"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ChapterUpdateRequestValidator()
    {
        RuleFor(x => x.Baslik)
            .NotEmpty()
            .WithMessage("Bölüm başlığı boş olamaz.")
            .MaximumLength(500)
            .WithMessage("Bölüm başlığı en fazla 500 karakter olabilir.");

        RuleFor(x => x.TemizMetin)
            .NotEmpty()
            .WithMessage("Bölüm metni boş olamaz.");

        RuleFor(x => x.MetinSurumu)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Metin sürümü 1 veya daha büyük olmalıdır.");
    }
}

/// <summary>
/// <see cref="ChapterMergeRequest"/> doğrulama kuralları.
/// </summary>
public sealed class ChapterMergeRequestValidator : AbstractValidator<ChapterMergeRequest>
{
    /// <summary>
    /// <see cref="ChapterMergeRequestValidator"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ChapterMergeRequestValidator()
    {
        RuleFor(x => x.ChapterIds)
            .NotNull()
            .WithMessage("Birleştirilecek bölüm listesi belirtilmelidir.")
            .Must(ids => ids is { Count: >= 2 and <= 100 })
            .WithMessage("En az 2, en fazla 100 ardışık bölüm birleştirilebilir.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Birleştirilecek bölüm kimlikleri benzersiz olmalıdır.");
    }
}

/// <summary>
/// <see cref="ChapterSplitRequest"/> doğrulama kuralları.
/// </summary>
public sealed class ChapterSplitRequestValidator : AbstractValidator<ChapterSplitRequest>
{
    /// <summary>
    /// <see cref="ChapterSplitRequestValidator"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ChapterSplitRequestValidator()
    {
        RuleFor(x => x.KarakterIndeksi)
            .GreaterThan(0)
            .WithMessage("Bölme karakter indeksi 0'dan büyük olmalıdır.");

        RuleFor(x => x.YeniBaslik)
            .MaximumLength(500)
            .WithMessage("Yeni bölüm başlığı en fazla 500 karakter olabilir.");
    }
}

/// <summary>
/// <see cref="ChapterOrderRequest"/> doğrulama kuralları.
/// </summary>
public sealed class ChapterOrderRequestValidator : AbstractValidator<ChapterOrderRequest>
{
    /// <summary>
    /// <see cref="ChapterOrderRequestValidator"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ChapterOrderRequestValidator()
    {
        RuleFor(x => x.ChapterIds)
            .NotNull()
            .WithMessage("Bölüm sıralama listesi boş olamaz.")
            .Must(ids => ids is { Count: > 0 and <= 2000 })
            .WithMessage("Bölüm listesi boş olamaz ve 2000'i geçemez.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .WithMessage("Bölüm kimlikleri benzersiz olmalıdır.");
    }
}

/// <summary>
/// <see cref="ChapterInclusionRequest"/> doğrulama kuralları.
/// </summary>
public sealed class ChapterInclusionRequestValidator : AbstractValidator<ChapterInclusionRequest>
{
    /// <summary>
    /// <see cref="ChapterInclusionRequestValidator"/> sınıfının yeni bir örneğini başlatır.
    /// </summary>
    public ChapterInclusionRequestValidator()
    {
        RuleFor(x => x.ChapterIds)
            .NotNull()
            .WithMessage("Bölüm listesi boş olamaz.")
            .Must(ids => ids is { Count: > 0 and <= 2000 })
            .WithMessage("Bölüm listesi boş olamaz ve 2000'i geçemez.");
    }
}
