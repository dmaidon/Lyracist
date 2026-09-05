using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lyracist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMeasuredLoudnessToSong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MeasuredLoudnessLufs",
                table: "Songs",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MeasuredLoudnessLufs",
                table: "Songs");
        }
    }
}
