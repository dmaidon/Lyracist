// Edited on Aug 25, 2026 @ 06:15:00 -> Fix RCS1021 expression-bodied lambda syntax
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Lyracist.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OccasionCategories",
                columns: table => new
                {
                    OccasionCategoryId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ParentCategoryId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OccasionCategories", x => x.OccasionCategoryId);
                    table.ForeignKey(
                        name: "FK_OccasionCategories_OccasionCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "OccasionCategories",
                        principalColumn: "OccasionCategoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Singers",
                columns: table => new
                {
                    SingerId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    JoinDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSang = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalSongsSung = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    RatingPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    RatingCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageRating = table.Column<double>(type: "REAL", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    PinCode = table.Column<string>(type: "TEXT", nullable: false),
                    AvatarType = table.Column<string>(type: "TEXT", nullable: false),
                    AvatarSource = table.Column<string>(type: "TEXT", nullable: false),
                    VocalRange = table.Column<string>(type: "TEXT", nullable: false),
                    CustomTitle = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Singers", x => x.SingerId));

            migrationBuilder.CreateTable(
                name: "Songs",
                columns: table => new
                {
                    SongId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Artist = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    IsKaraoke = table.Column<bool>(type: "INTEGER", nullable: false),
                    KaraokeType = table.Column<string>(type: "TEXT", nullable: false),
                    Genre = table.Column<string>(type: "TEXT", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<double>(type: "REAL", nullable: false),
                    KeyDefault = table.Column<int>(type: "INTEGER", nullable: false),
                    TempoDefault = table.Column<double>(type: "REAL", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastPlayed = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PlayCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Songs", x => x.SongId));

            migrationBuilder.Sql("CREATE VIRTUAL TABLE SongSearch USING fts5(SongId UNINDEXED, Title, Artist, NormalizedTitle, NormalizedArtist);");

            migrationBuilder.CreateTable(
                name: "OccasionItems",
                columns: table => new
                {
                    OccasionItemId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OccasionCategoryId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    Treble = table.Column<double>(type: "REAL", nullable: false),
                    Mid = table.Column<double>(type: "REAL", nullable: false),
                    Bass = table.Column<double>(type: "REAL", nullable: false),
                    Gain = table.Column<double>(type: "REAL", nullable: false),
                    Key = table.Column<int>(type: "INTEGER", nullable: false),
                    Tempo = table.Column<double>(type: "REAL", nullable: false),
                    Compressor = table.Column<double>(type: "REAL", nullable: false),
                    Limiter = table.Column<double>(type: "REAL", nullable: false),
                    Icon = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OccasionItems", x => x.OccasionItemId);
                    table.ForeignKey(
                        name: "FK_OccasionItems_OccasionCategories_OccasionCategoryId",
                        column: x => x.OccasionCategoryId,
                        principalTable: "OccasionCategories",
                        principalColumn: "OccasionCategoryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MusicRequests",
                columns: table => new
                {
                    MusicRequestId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SingerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Artist = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    RequestType = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicRequests", x => x.MusicRequestId);
                    table.ForeignKey(
                        name: "FK_MusicRequests_Singers_SingerId",
                        column: x => x.SingerId,
                        principalTable: "Singers",
                        principalColumn: "SingerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SingerAudioSettings",
                columns: table => new
                {
                    SingerAudioSettingsId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SingerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Treble = table.Column<double>(type: "REAL", nullable: false),
                    Mid = table.Column<double>(type: "REAL", nullable: false),
                    Bass = table.Column<double>(type: "REAL", nullable: false),
                    Gain = table.Column<double>(type: "REAL", nullable: false),
                    Key = table.Column<int>(type: "INTEGER", nullable: false),
                    Tempo = table.Column<double>(type: "REAL", nullable: false),
                    Compressor = table.Column<double>(type: "REAL", nullable: false),
                    Limiter = table.Column<double>(type: "REAL", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SingerAudioSettings", x => x.SingerAudioSettingsId);
                    table.ForeignKey(
                        name: "FK_SingerAudioSettings_Singers_SingerId",
                        column: x => x.SingerId,
                        principalTable: "Singers",
                        principalColumn: "SingerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EndRotationPlaylistItems",
                columns: table => new
                {
                    EndRotationPlaylistItemId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SongId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndRotationPlaylistItems", x => x.EndRotationPlaylistItemId);
                    table.ForeignKey(
                        name: "FK_EndRotationPlaylistItems_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "SongId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FillInPlaylistItems",
                columns: table => new
                {
                    FillInPlaylistItemId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SongId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FillInPlaylistItems", x => x.FillInPlaylistItemId);
                    table.ForeignKey(
                        name: "FK_FillInPlaylistItems_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "SongId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpeningPlaylistItems",
                columns: table => new
                {
                    OpeningPlaylistItemId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SongId = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningPlaylistItems", x => x.OpeningPlaylistItemId);
                    table.ForeignKey(
                        name: "FK_OpeningPlaylistItems_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "SongId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RotationEntries",
                columns: table => new
                {
                    RotationEntryId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SingerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SongId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedKey = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedTempo = table.Column<double>(type: "REAL", nullable: false),
                    TimestampAdded = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RotationEntries", x => x.RotationEntryId);
                    table.ForeignKey(
                        name: "FK_RotationEntries_Singers_SingerId",
                        column: x => x.SingerId,
                        principalTable: "Singers",
                        principalColumn: "SingerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RotationEntries_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "SongId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SongAudioSettings",
                columns: table => new
                {
                    SongAudioSettingsId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SongId = table.Column<int>(type: "INTEGER", nullable: false),
                    Treble = table.Column<double>(type: "REAL", nullable: false),
                    Mid = table.Column<double>(type: "REAL", nullable: false),
                    Bass = table.Column<double>(type: "REAL", nullable: false),
                    Gain = table.Column<double>(type: "REAL", nullable: false),
                    Key = table.Column<int>(type: "INTEGER", nullable: false),
                    Tempo = table.Column<double>(type: "REAL", nullable: false),
                    Compressor = table.Column<double>(type: "REAL", nullable: false),
                    Limiter = table.Column<double>(type: "REAL", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongAudioSettings", x => x.SongAudioSettingsId);
                    table.ForeignKey(
                        name: "FK_SongAudioSettings_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "SongId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "OccasionCategories",
                columns: new[] { "OccasionCategoryId", "Name", "ParentCategoryId" },
                values: new object[,]
                {
                    { 1, "Holiday", null },
                    { 2, "Birthday", null },
                    { 3, "Anniversary", null },
                    { 4, "Wedding", null },
                    { 5, "Graduation", null },
                    { 6, "Retirement", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EndRotationPlaylistItems_SongId",
                table: "EndRotationPlaylistItems",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_FillInPlaylistItems_SongId",
                table: "FillInPlaylistItems",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_MusicRequests_SingerId",
                table: "MusicRequests",
                column: "SingerId");

            migrationBuilder.CreateIndex(
                name: "IX_OccasionCategories_ParentCategoryId",
                table: "OccasionCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_OccasionItems_OccasionCategoryId",
                table: "OccasionItems",
                column: "OccasionCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningPlaylistItems_SongId",
                table: "OpeningPlaylistItems",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_RotationEntries_SingerId",
                table: "RotationEntries",
                column: "SingerId");

            migrationBuilder.CreateIndex(
                name: "IX_RotationEntries_SongId",
                table: "RotationEntries",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_SingerAudioSettings_SingerId",
                table: "SingerAudioSettings",
                column: "SingerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SongAudioSettings_SongId",
                table: "SongAudioSettings",
                column: "SongId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Songs_Artist",
                table: "Songs",
                column: "Artist");

            migrationBuilder.CreateIndex(
                name: "IX_Songs_FilePath",
                table: "Songs",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Songs_Title",
                table: "Songs",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EndRotationPlaylistItems");

            migrationBuilder.DropTable(
                name: "FillInPlaylistItems");

            migrationBuilder.DropTable(
                name: "MusicRequests");

            migrationBuilder.DropTable(
                name: "OccasionItems");

            migrationBuilder.DropTable(
                name: "OpeningPlaylistItems");

            migrationBuilder.DropTable(
                name: "RotationEntries");

            migrationBuilder.DropTable(
                name: "SingerAudioSettings");

            migrationBuilder.DropTable(
                name: "SongAudioSettings");

            migrationBuilder.DropTable(
                name: "SongSearch");

            migrationBuilder.DropTable(
                name: "OccasionCategories");

            migrationBuilder.DropTable(
                name: "Singers");

            migrationBuilder.DropTable(
                name: "Songs");
        }
    }
}
