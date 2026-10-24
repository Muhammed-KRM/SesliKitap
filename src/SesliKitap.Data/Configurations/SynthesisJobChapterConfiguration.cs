using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Configurations;

/// <summary>
/// <see cref="SynthesisJobChapter"/> varlığı için EF Core tablo ve bileşik anahtar yapılandırması (Bölüm 4.15.5).
/// </summary>
public sealed class SynthesisJobChapterConfiguration : IEntityTypeConfiguration<SynthesisJobChapter>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SynthesisJobChapter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("synthesis_job_chapters");
        builder.HasKey(x => new { x.JobId, x.ChapterId });

        builder.Property(x => x.HataMesaji)
            .HasColumnType("text");

        builder.HasOne(x => x.Job)
            .WithMany(x => x.Bolumler)
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Chapter)
            .WithMany()
            .HasForeignKey(x => x.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => x.Chapter == null || x.Chapter.Book == null || !x.Chapter.Book.SilindiMi);
    }
}
