using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Configurations;

/// <summary>
/// <see cref="Book"/> varlığı için veritabanı tablo, kolon ve indeks yapılandırması.
/// </summary>
public sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Baslik)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Yazar)
            .HasMaxLength(300);

        builder.Property(x => x.Aciklama)
            .HasColumnType("text");

        builder.Property(x => x.Dil)
            .HasMaxLength(10)
            .HasDefaultValue("tr")
            .IsRequired();

        builder.Property(x => x.KaynakKimligi)
            .HasMaxLength(300);

        builder.Property(x => x.KaynakUrl)
            .HasMaxLength(2000);

        builder.Property(x => x.OrijinalDosyaYolu)
            .HasMaxLength(500);

        builder.Property(x => x.OrijinalDosyaAdi)
            .HasMaxLength(500);

        builder.Property(x => x.DosyaHash)
            .HasMaxLength(64)
            .IsFixedLength();

        builder.Property(x => x.KapakYolu)
            .HasMaxLength(500);

        builder.Property(x => x.HataMesaji)
            .HasColumnType("text");

        builder.Property(x => x.Metadata)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.DosyaHash)
            .IsUnique()
            .HasFilter("silindi_mi = false AND dosya_hash IS NOT NULL")
            .HasDatabaseName("idx_books_dosya_hash_unique");

        builder.HasIndex(x => x.Durum)
            .HasDatabaseName("idx_books_durum");

        builder.HasIndex(x => x.SonDinlenmeTarihi)
            .HasDatabaseName("idx_books_son_dinlenme_tarihi")
            .IsDescending();

        builder.HasIndex(x => x.OlusturmaTarihi)
            .HasDatabaseName("idx_books_olusturma_tarihi")
            .IsDescending();

        builder.HasGeneratedTsVectorColumn(
                x => x.AramaVektoru!,
                "simple",
                x => new { x.Baslik, x.Yazar })
            .HasIndex(x => x.AramaVektoru)
            .HasMethod("GIN")
            .HasDatabaseName("idx_books_arama_vektoru");
    }
}
