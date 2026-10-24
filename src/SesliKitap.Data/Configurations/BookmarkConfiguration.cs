using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Configurations;

/// <summary>
/// <see cref="Bookmark"/> varlığı için Entity Framework Core tablo ve ilişki yapılandırması.
/// </summary>
public sealed class BookmarkConfiguration : IEntityTypeConfiguration<Bookmark>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.ToTable("bookmarks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PozisyonSn)
            .IsRequired();

        builder.Property(x => x.Not)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.BookId, x.OlusturmaTarihi });
        builder.HasIndex(x => x.ChapterId);

        builder.HasOne(x => x.Book)
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Chapter)
            .WithMany()
            .HasForeignKey(x => x.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
