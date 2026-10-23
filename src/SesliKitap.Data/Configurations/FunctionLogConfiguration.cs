using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SesliKitap.Data.Entities;

namespace SesliKitap.Data.Configurations;

/// <summary>
/// <see cref="FunctionLog"/> varlığı için veritabanı tablo, kolon ve indeks yapılandırması.
/// </summary>
public sealed class FunctionLogConfiguration : IEntityTypeConfiguration<FunctionLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<FunctionLog> builder)
    {
        builder.ToTable("function_logs");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();

        builder.Property(f => f.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(f => f.ClassName)
            .HasColumnName("class_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.MethodName)
            .HasColumnName("method_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.FilePath)
            .HasColumnName("file_path")
            .HasMaxLength(500);

        builder.Property(f => f.LineNumber)
            .HasColumnName("line_number");

        builder.Property(f => f.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        builder.Property(f => f.StackTrace)
            .HasColumnName("stack_trace")
            .HasColumnType("text");

        builder.Property(f => f.InputType)
            .HasColumnName("input_type")
            .HasMaxLength(200);

        builder.Property(f => f.InputValue)
            .HasColumnName("input_value")
            .HasColumnType("text");

        builder.Property(f => f.TraceId)
            .HasColumnName("trace_id")
            .HasMaxLength(100);

        builder.Property(f => f.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .HasDefaultValue("Error")
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasIndex(f => f.CreatedAt)
            .HasDatabaseName("idx_function_logs_created_at")
            .IsDescending();

        builder.HasIndex(f => f.ErrorCode)
            .HasDatabaseName("idx_function_logs_error_code");

        builder.HasIndex(f => f.TraceId)
            .HasDatabaseName("idx_function_logs_trace_id");
    }
}
