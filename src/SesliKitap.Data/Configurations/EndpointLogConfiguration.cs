using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Configurations;

/// <summary>
/// <see cref="EndpointLog"/> varlığı için veritabanı tablo, kolon ve indeks yapılandırması.
/// </summary>
public sealed class EndpointLogConfiguration : IEntityTypeConfiguration<EndpointLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<EndpointLog> builder)
    {
        builder.ToTable("endpoint_logs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(e => e.TraceId)
            .HasColumnName("trace_id")
            .HasMaxLength(100);

        builder.Property(e => e.Method)
            .HasColumnName("method")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(e => e.Path)
            .HasColumnName("path")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Query)
            .HasColumnName("query")
            .HasMaxLength(2000);

        builder.Property(e => e.RequestBody)
            .HasColumnName("request_body")
            .HasColumnType("text");

        builder.Property(e => e.ResponseBody)
            .HasColumnName("response_body")
            .HasColumnType("text");

        builder.Property(e => e.StatusCode)
            .HasColumnName("status_code")
            .IsRequired();

        builder.Property(e => e.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(64);

        builder.Property(e => e.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(e => e.DurationMs)
            .HasColumnName("duration_ms")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("idx_endpoint_logs_created_at")
            .IsDescending();

        builder.HasIndex(e => e.TraceId)
            .HasDatabaseName("idx_endpoint_logs_trace_id");

        builder.HasIndex(e => e.StatusCode)
            .HasDatabaseName("idx_endpoint_logs_status_code");
    }
}
