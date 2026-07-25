using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lyracist.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeSongFilePathIndexUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Songs_FilePath",
                table: "Songs");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_FilePath",
                table: "Songs",
                column: "FilePath",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Songs_FilePath",
                table: "Songs");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_FilePath",
                table: "Songs",
                column: "FilePath");
        }
    }
}
