using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Configurations;

/// <summary>
/// <see cref="VoiceProfile"/> varlığı için EF Core tablo, kolon ve indeks yapılandırması.
/// </summary>
public sealed class VoiceProfileConfiguration : IEntityTypeConfiguration<VoiceProfile>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<VoiceProfile> builder)
    {
        builder.ToTable("voice_profiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Ad)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Aciklama)
            .HasMaxLength(500);

        builder.Property(x => x.HamSesYolu)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.ReferansSesYolu)
            .HasMaxLength(500);

        builder.Property(x => x.ReferansHash)
            .HasMaxLength(64)
            .IsFixedLength();

        builder.Property(x => x.OrnekSesYolu)
            .HasMaxLength(500);

        builder.OwnsOne(x => x.Parametreler, owned =>
        {
            owned.ToJson("parametreler");
        });

        builder.HasIndex(x => x.Ad)
            .IsUnique()
            .HasDatabaseName("ix_voice_profiles_ad_unique")
            .HasFilter("silindi_mi = false");

        builder.HasIndex(x => x.VarsayilanMi)
            .IsUnique()
            .HasDatabaseName("ix_voice_profiles_varsayilan_unique")
            .HasFilter("varsayilan_mi = true AND silindi_mi = false");

        builder.HasIndex(x => x.Durum)
            .HasDatabaseName("ix_voice_profiles_durum");
    }
}
