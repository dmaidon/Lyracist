// Created on Oct 7, 2026 @ 19:45:00 -> Migration adding MixInMs, MixOutMs, AllowRecording, and PerformanceRecordings
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lyracist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFillerAutomixAndRecording : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MixInMs",
                table: "Songs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MixOutMs",
                table: "Songs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowRecording",
                table: "Singers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PerformanceRecordings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SingerName = table.Column<string>(type: "TEXT", nullable: false),
                    SingerId = table.Column<int>(type: "INTEGER", nullable: true),
                    SongTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Artist = table.Column<string>(type: "TEXT", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceRecordings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerformanceRecordings");

            migrationBuilder.DropColumn(
                name: "MixInMs",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "MixOutMs",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "AllowRecording",
                table: "Singers");
        }
    }
}
