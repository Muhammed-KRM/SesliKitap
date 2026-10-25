using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace SesliKitap.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "arama_vektoru",
                table: "books",
                type: "tsvector",
                nullable: true)
                .Annotation("Npgsql:TsVectorConfig", "simple")
                .Annotation("Npgsql:TsVectorProperties", new[] { "baslik", "yazar" });

            migrationBuilder.CreateIndex(
                name: "idx_books_arama_vektoru",
                table: "books",
                column: "arama_vektoru")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_books_arama_vektoru",
                table: "books");

            migrationBuilder.DropColumn(
                name: "arama_vektoru",
                table: "books");
        }
    }
}
