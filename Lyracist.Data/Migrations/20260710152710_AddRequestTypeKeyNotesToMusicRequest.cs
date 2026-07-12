using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lyracist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestTypeKeyNotesToMusicRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "MusicRequests",
                type: "TEXT",
                nullable: false,
                defaultValue: "0");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "MusicRequests",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestType",
                table: "MusicRequests",
                type: "TEXT",
                nullable: false,
                defaultValue: "Karaoke");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Key",
                table: "MusicRequests");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "MusicRequests");

            migrationBuilder.DropColumn(
                name: "RequestType",
                table: "MusicRequests");
        }
    }
}
