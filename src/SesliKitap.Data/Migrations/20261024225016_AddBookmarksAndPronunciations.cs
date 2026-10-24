using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SesliKitap.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookmarksAndPronunciations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bookmarks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pozisyon_sn = table.Column<double>(type: "double precision", nullable: false),
                    not = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookmarks", x => x.id);
                    table.ForeignKey(
                        name: "FK_bookmarks_book_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalTable: "book_chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bookmarks_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pronunciation_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kelime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    okunus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    buyuk_kucuk_duyarli = table.Column<bool>(type: "boolean", nullable: false),
                    aktif_mi = table.Column<bool>(type: "boolean", nullable: false),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pronunciation_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookmarks_book_id_olusturma_tarihi",
                table: "bookmarks",
                columns: new[] { "book_id", "olusturma_tarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_bookmarks_chapter_id",
                table: "bookmarks",
                column: "chapter_id");

            migrationBuilder.CreateIndex(
                name: "IX_pronunciation_entries_aktif_mi",
                table: "pronunciation_entries",
                column: "aktif_mi");

            migrationBuilder.CreateIndex(
                name: "IX_pronunciation_entries_kelime",
                table: "pronunciation_entries",
                column: "kelime",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bookmarks");

            migrationBuilder.DropTable(
                name: "pronunciation_entries");
        }
    }
}
