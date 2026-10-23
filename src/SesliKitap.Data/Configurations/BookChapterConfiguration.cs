using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Configurations;

/// <summary>
/// <see cref="BookChapter"/> varlığı için veritabanı tablo, kolon ve indeks yapılandırması.
/// </summary>
public sealed class BookChapterConfiguration : IEntityTypeConfiguration<BookChapter>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BookChapter> builder)
    {
        builder.ToTable("book_chapters");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Baslik)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.TemizMetin)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(x => x.OtomatikHaricNedeni)
            .HasMaxLength(100);

        builder.Property(x => x.OpusYolu)
            .HasMaxLength(500);

        builder.Property(x => x.Mp3Yolu)
            .HasMaxLength(500);

        builder.Property(x => x.ZamanDamgasiYolu)
            .HasMaxLength(500);

        builder.Property(x => x.HataMesaji)
            .HasColumnType("text");

        builder.HasIndex(x => new { x.BookId, x.SiraNo })
            .IsUnique()
            .HasDatabaseName("idx_book_chapters_book_id_sira_no");

        builder.HasIndex(x => new { x.BookId, x.DahilMi })
            .HasDatabaseName("idx_book_chapters_book_id_dahil_mi");

        builder.HasIndex(x => x.SesDurumu)
            .HasDatabaseName("idx_book_chapters_ses_durumu");

        builder.HasOne(x => x.Book)
            .WithMany(x => x.Bolumler)
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.Book.SilindiMi);
    }
}
