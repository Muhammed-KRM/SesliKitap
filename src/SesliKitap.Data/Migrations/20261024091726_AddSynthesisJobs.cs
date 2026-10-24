using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SesliKitap.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSynthesisJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "synthesis_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voice_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    motor_sirasi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    kullanilan_motorlar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    durum = table.Column<int>(type: "integer", nullable: false),
                    toplam_bolum = table.Column<int>(type: "integer", nullable: false),
                    tamamlanan_bolum = table.Column<int>(type: "integer", nullable: false),
                    hatali_bolum = table.Column<int>(type: "integer", nullable: false),
                    yuzde = table.Column<int>(type: "integer", nullable: false),
                    mevcut_bolum_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mesaj = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    toplam_parca = table.Column<int>(type: "integer", nullable: false),
                    onbellekten_parca = table.Column<int>(type: "integer", nullable: false),
                    uretilen_parca = table.Column<int>(type: "integer", nullable: false),
                    gpu_ms = table.Column<long>(type: "bigint", nullable: false),
                    tahmini_maliyet_usd = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    tahmini_sure_sn = table.Column<double>(type: "double precision", nullable: true),
                    uretilen_ses_sn = table.Column<double>(type: "double precision", nullable: false),
                    trace_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    baslangic_zamani = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    bitis_zamani = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    son_ilerleme_zamani = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    hata_mesaji = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ayarlar = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_synthesis_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_synthesis_jobs_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_synthesis_jobs_voice_profiles_voice_profile_id",
                        column: x => x.voice_profile_id,
                        principalTable: "voice_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "synthesis_job_chapters",
                columns: table => new
                {
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sira = table.Column<int>(type: "integer", nullable: false),
                    durum = table.Column<int>(type: "integer", nullable: false),
                    gpu_ms = table.Column<long>(type: "bigint", nullable: false),
                    hata_mesaji = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_synthesis_job_chapters", x => new { x.job_id, x.chapter_id });
                    table.ForeignKey(
                        name: "FK_synthesis_job_chapters_book_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalTable: "book_chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_synthesis_job_chapters_synthesis_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "synthesis_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_synthesis_job_chapters_chapter_id",
                table: "synthesis_job_chapters",
                column: "chapter_id");

            migrationBuilder.CreateIndex(
                name: "IX_synthesis_jobs_voice_profile_id",
                table: "synthesis_jobs",
                column: "voice_profile_id");

            migrationBuilder.CreateIndex(
                name: "idx_synthesis_jobs_book_id",
                table: "synthesis_jobs",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "idx_synthesis_jobs_durum",
                table: "synthesis_jobs",
                column: "durum");

            migrationBuilder.CreateIndex(
                name: "idx_synthesis_jobs_olusturma_tarihi",
                table: "synthesis_jobs",
                column: "olusturma_tarihi");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "synthesis_job_chapters");

            migrationBuilder.DropTable(
                name: "synthesis_jobs");
        }
    }
}
