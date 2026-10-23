using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Configurations;

/// <summary>
/// <see cref="PipelineLog"/> varlığı için veritabanı tablo, kolon ve indeks yapılandırması.
/// </summary>
public sealed class PipelineLogConfiguration : IEntityTypeConfiguration<PipelineLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PipelineLog> builder)
    {
        builder.ToTable("pipeline_logs");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(p => p.BookId)
            .HasColumnName("book_id");

        builder.Property(p => p.JobId)
            .HasColumnName("job_id");

        builder.Property(p => p.ChapterId)
            .HasColumnName("chapter_id");

        builder.Property(p => p.Asama)
            .HasColumnName("asama")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Durum)
            .HasColumnName("durum")
            .HasMaxLength(30)
            .HasDefaultValue("Basladi")
            .IsRequired();

        builder.Property(p => p.Yuzde)
            .HasColumnName("yuzde");

        builder.Property(p => p.Mesaj)
            .HasColumnName("mesaj")
            .HasMaxLength(1000);

        builder.Property(p => p.GirdiMetadata)
            .HasColumnName("girdi_metadata")
            .HasColumnType("jsonb");

        builder.Property(p => p.CiktiMetadata)
            .HasColumnName("cikti_metadata")
            .HasColumnType("jsonb");

        builder.Property(p => p.HataMesaji)
            .HasColumnName("hata_mesaji")
            .HasColumnType("text");

        builder.Property(p => p.HataDetayi)
            .HasColumnName("hata_detayi")
            .HasColumnType("text");

        builder.Property(p => p.Kaynak)
            .HasColumnName("kaynak")
            .HasMaxLength(30)
            .HasDefaultValue("Api")
            .IsRequired();

        builder.Property(p => p.TraceId)
            .HasColumnName("trace_id")
            .HasMaxLength(100);

        builder.Property(p => p.BaslangicZamani)
            .HasColumnName("baslangic_zamani")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(p => p.BitisZamani)
            .HasColumnName("bitis_zamani")
            .HasColumnType("timestamptz");

        builder.Property(p => p.SureMs)
            .HasColumnName("sure_ms");

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(p => p.BookId)
            .HasDatabaseName("idx_pipeline_logs_book_id");

        builder.HasIndex(p => p.JobId)
            .HasDatabaseName("idx_pipeline_logs_job_id");

        builder.HasIndex(p => p.TraceId)
            .HasDatabaseName("idx_pipeline_logs_trace_id");

        builder.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("idx_pipeline_logs_created_at")
            .IsDescending();
    }
}
