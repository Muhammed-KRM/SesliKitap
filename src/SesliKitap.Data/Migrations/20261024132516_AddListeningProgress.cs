using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SesliKitap.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListeningProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "listening_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pozisyon_sn = table.Column<double>(type: "double precision", nullable: false),
                    oynatma_hizi = table.Column<double>(type: "double precision", nullable: false),
                    tamamlanma_yuzdesi = table.Column<double>(type: "double precision", nullable: false),
                    cihaz_kimligi = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    cihaz_adi = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    istemci_zamani = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tamamlandi_mi = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_listening_progress", x => x.id);
                    table.ForeignKey(
                        name: "FK_listening_progress_book_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalTable: "book_chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_listening_progress_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_listening_progress_book_id",
                table: "listening_progress",
                column: "book_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_listening_progress_chapter_id",
                table: "listening_progress",
                column: "chapter_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listening_progress");
        }
    }
}
