using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SesliKitap.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "voice_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ad = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aciklama = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cinsiyet = table.Column<int>(type: "integer", nullable: true),
                    ham_ses_yolu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    referans_ses_yolu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    referans_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    referans_metin = table.Column<string>(type: "text", nullable: true),
                    referans_sure_sn = table.Column<double>(type: "double precision", nullable: true),
                    ornek_ses_yolu = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    varsayilan_mi = table.Column<bool>(type: "boolean", nullable: false),
                    kullanim_onayi = table.Column<bool>(type: "boolean", nullable: false),
                    durum = table.Column<int>(type: "integer", nullable: false),
                    hata_mesaji = table.Column<string>(type: "text", nullable: true),
                    gpu_ms = table.Column<long>(type: "bigint", nullable: false),
                    silindi_mi = table.Column<bool>(type: "boolean", nullable: false),
                    silinme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    olusturma_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    guncelleme_tarihi = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    parametreler = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_voice_profiles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_voice_profiles_ad_unique",
                table: "voice_profiles",
                column: "ad",
                unique: true,
                filter: "silindi_mi = false");

            migrationBuilder.CreateIndex(
                name: "ix_voice_profiles_durum",
                table: "voice_profiles",
                column: "durum");

            migrationBuilder.CreateIndex(
                name: "ix_voice_profiles_varsayilan_unique",
                table: "voice_profiles",
                column: "varsayilan_mi",
                unique: true,
                filter: "varsayilan_mi = true AND silindi_mi = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "voice_profiles");
        }
    }
}
