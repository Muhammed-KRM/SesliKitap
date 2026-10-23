using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SesliKitap.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "endpoint_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    trace_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    query = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    request_body = table.Column<string>(type: "text", nullable: true),
                    response_body = table.Column<string>(type: "text", nullable: true),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_endpoint_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "function_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    class_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    method_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    stack_trace = table.Column<string>(type: "text", nullable: true),
                    input_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    input_value = table.Column<string>(type: "text", nullable: true),
                    trace_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Error"),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_function_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pipeline_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    book_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    asama = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    durum = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Basladi"),
                    yuzde = table.Column<int>(type: "integer", nullable: true),
                    mesaj = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    girdi_metadata = table.Column<string>(type: "jsonb", nullable: true),
                    cikti_metadata = table.Column<string>(type: "jsonb", nullable: true),
                    hata_mesaji = table.Column<string>(type: "text", nullable: true),
                    hata_detayi = table.Column<string>(type: "text", nullable: true),
                    kaynak = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Api"),
                    trace_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    baslangic_zamani = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    bitis_zamani = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    sure_ms = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipeline_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_endpoint_logs_created_at",
                table: "endpoint_logs",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_endpoint_logs_status_code",
                table: "endpoint_logs",
                column: "status_code");

            migrationBuilder.CreateIndex(
                name: "idx_endpoint_logs_trace_id",
                table: "endpoint_logs",
                column: "trace_id");

            migrationBuilder.CreateIndex(
                name: "idx_function_logs_created_at",
                table: "function_logs",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_function_logs_error_code",
                table: "function_logs",
                column: "error_code");

            migrationBuilder.CreateIndex(
                name: "idx_function_logs_trace_id",
                table: "function_logs",
                column: "trace_id");

            migrationBuilder.CreateIndex(
                name: "idx_pipeline_logs_book_id",
                table: "pipeline_logs",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "idx_pipeline_logs_created_at",
                table: "pipeline_logs",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_pipeline_logs_job_id",
                table: "pipeline_logs",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "idx_pipeline_logs_trace_id",
                table: "pipeline_logs",
                column: "trace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "endpoint_logs");

            migrationBuilder.DropTable(
                name: "function_logs");

            migrationBuilder.DropTable(
                name: "pipeline_logs");
        }
    }
}
