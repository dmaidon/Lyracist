using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lyracist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSingerRatingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageRating",
                table: "Singers",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "RatingCount",
                table: "Singers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RatingPoints",
                table: "Singers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "Singers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "Singers");

            migrationBuilder.DropColumn(
                name: "RatingCount",
                table: "Singers");

            migrationBuilder.DropColumn(
                name: "RatingPoints",
                table: "Singers");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "Singers");
        }
    }
}
