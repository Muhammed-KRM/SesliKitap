using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Configurations;

/// <summary>
/// <see cref="ListeningProgress"/> varlığı için EF Core tablo ve indeks yapılandırması.
/// </summary>
public sealed class ListeningProgressConfiguration : IEntityTypeConfiguration<ListeningProgress>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ListeningProgress> builder)
    {
        builder.ToTable("listening_progress");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CihazKimligi)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.CihazAdi)
            .HasMaxLength(100);

        builder.Property(x => x.Xmin)
            .IsRowVersion();

        builder.HasIndex(x => x.BookId)
            .IsUnique();

        builder.HasOne(x => x.Book)
            .WithOne(b => b.Konum)
            .HasForeignKey<ListeningProgress>(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Chapter)
            .WithMany()
            .HasForeignKey(x => x.ChapterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => x.Book == null || !x.Book.SilindiMi);
    }
}
