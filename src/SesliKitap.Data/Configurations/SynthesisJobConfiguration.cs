using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Configurations;

/// <summary>
/// <see cref="SynthesisJob"/> varlığı için EF Core tablo, kolon ve indeks yapılandırması (Bölüm 4.15.5).
/// </summary>
public sealed class SynthesisJobConfiguration : IEntityTypeConfiguration<SynthesisJob>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SynthesisJob> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("synthesis_jobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MotorSirasi)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.KullanilanMotorlar)
            .HasMaxLength(200);

        builder.Property(x => x.Mesaj)
            .HasMaxLength(500);

        builder.Property(x => x.TahminiMaliyetUsd)
            .HasPrecision(10, 4);

        builder.Property(x => x.TraceId)
            .HasMaxLength(100);

        builder.Property(x => x.HataMesaji)
            .HasColumnType("text");

        builder.Property(x => x.Xmin)
            .IsRowVersion();

        builder.OwnsOne(x => x.Ayarlar, owned =>
        {
            owned.ToJson("ayarlar");
        });

        builder.HasOne(x => x.Book)
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => x.Book == null || !x.Book.SilindiMi);

        builder.HasOne(x => x.VoiceProfile)
            .WithMany()
            .HasForeignKey(x => x.VoiceProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Durum)
            .HasDatabaseName("idx_synthesis_jobs_durum");

        builder.HasIndex(x => x.BookId)
            .HasDatabaseName("idx_synthesis_jobs_book_id");

        builder.HasIndex(x => x.OlusturmaTarihi)
            .HasDatabaseName("idx_synthesis_jobs_olusturma_tarihi");
    }
}
