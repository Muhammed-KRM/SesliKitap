using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Configurations;

/// <summary>
/// <see cref="PronunciationEntry"/> varlığı için Entity Framework Core tablo yapılandırması.
/// </summary>
public sealed class PronunciationEntryConfiguration : IEntityTypeConfiguration<PronunciationEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PronunciationEntry> builder)
    {
        builder.ToTable("pronunciation_entries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kelime)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Okunus)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.BuyukKucukDuyarli)
            .IsRequired();

        builder.Property(x => x.AktifMi)
            .IsRequired();

        builder.Property(x => x.Aciklama)
            .HasMaxLength(500);

        builder.HasIndex(x => x.Kelime)
            .IsUnique();

        builder.HasIndex(x => x.AktifMi);
    }
}
