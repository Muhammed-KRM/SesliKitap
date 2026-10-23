using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SesliKitap.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBooksAndChapters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "books",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    baslik = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    yazar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    aciklama = table.Column<string>(type: "text", nullable: true),
                    dil = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "tr"),
                    yayin_yili = table.Column<int>(type: "integer", nullable: true),
                    kaynak_tipi = table.Column<int>(type: "integer", nullable: false),
                    kaynak_kimligi = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    kaynak_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    dosya_formati = table.Column<int>(type: "integer", nullable: false),
                    orijinal_dosya_yolu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    orijinal_dosya_adi = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dosya_boyutu = table.Column<long>(type: "bigint", nullable: false),
                    dosya_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    kapak_yolu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sayfa_sayisi = table.Column<int>(type: "integer", nullable: true),
                    kelime_sayisi = table.Column<int>(type: "integer", nullable: false),
                    toplam_sure_sn = table.Column<double>(type: "double precision", nullable: false),
                    durum = table.Column<int>(type: "integer", nullable: false),
                    hata_mesaji = table.Column<string>(type: "text", nullable: true),
                    varsayilan_ses_profili_id = table.Column<Guid>(type: "uuid", nullable: true),
                    son_dinlenme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    silindi_mi = table.Column<bool>(type: "boolean", nullable: false),
                    silinme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_books", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "book_chapters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sira_no = table.Column<int>(type: "integer", nullable: false),
                    baslik = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    temiz_metin = table.Column<string>(type: "text", nullable: false),
                    kelime_sayisi = table.Column<int>(type: "integer", nullable: false),
                    karakter_sayisi = table.Column<int>(type: "integer", nullable: false),
                    dahil_mi = table.Column<bool>(type: "boolean", nullable: false),
                    otomatik_haric_nedeni = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    metin_surumu = table.Column<int>(type: "integer", nullable: false),
                    ses_durumu = table.Column<int>(type: "integer", nullable: false),
                    ses_metin_surumu = table.Column<int>(type: "integer", nullable: true),
                    ses_profili_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ses_motoru = table.Column<int>(type: "integer", nullable: true),
                    opus_yolu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    mp3_yolu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    zaman_damgasi_yolu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    opus_boyutu = table.Column<long>(type: "bigint", nullable: true),
                    mp3_boyutu = table.Column<long>(type: "bigint", nullable: true),
                    sure_sn = table.Column<double>(type: "double precision", nullable: true),
                    cumle_sayisi = table.Column<int>(type: "integer", nullable: true),
                    gpu_ms = table.Column<long>(type: "bigint", nullable: false),
                    hata_mesaji = table.Column<string>(type: "text", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_chapters", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_chapters_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_book_chapters_book_id_dahil_mi",
                table: "book_chapters",
                columns: new[] { "book_id", "dahil_mi" });

            migrationBuilder.CreateIndex(
                name: "idx_book_chapters_book_id_sira_no",
                table: "book_chapters",
                columns: new[] { "book_id", "sira_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_book_chapters_ses_durumu",
                table: "book_chapters",
                column: "ses_durumu");

            migrationBuilder.CreateIndex(
                name: "idx_books_dosya_hash_unique",
                table: "books",
                column: "dosya_hash",
                unique: true,
                filter: "silindi_mi = false AND dosya_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_books_durum",
                table: "books",
                column: "durum");

            migrationBuilder.CreateIndex(
                name: "idx_books_olusturma_tarihi",
                table: "books",
                column: "olusturma_tarihi",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_books_son_dinlenme_tarihi",
                table: "books",
                column: "son_dinlenme_tarihi",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_chapters");

            migrationBuilder.DropTable(
                name: "books");
        }
    }
}
