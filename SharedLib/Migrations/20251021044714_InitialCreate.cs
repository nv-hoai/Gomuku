using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SharedLib.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Salt = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "GameHistories",
                columns: table => new
                {
                    GameId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Player1Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Player2Id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsAIGame = table.Column<bool>(type: "bit", nullable: false),
                    Winner = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Player1Symbol = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Player2Symbol = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    GameStatus = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameHistories", x => x.GameId);
                    table.ForeignKey(
                        name: "FK_GameHistories_Users_Player1Id",
                        column: x => x.Player1Id,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameHistories_Users_Player2Id",
                        column: x => x.Player2Id,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerProfiles",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Avatar = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PlayerLevel = table.Column<int>(type: "int", nullable: false),
                    EloRating = table.Column<int>(type: "int", nullable: false),
                    TotalGamesPlayed = table.Column<int>(type: "int", nullable: false),
                    Wins = table.Column<int>(type: "int", nullable: false),
                    Losses = table.Column<int>(type: "int", nullable: false),
                    Draws = table.Column<int>(type: "int", nullable: false),
                    PreferredSymbol = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerProfiles", x => x.ProfileId);
                    table.ForeignKey(
                        name: "FK_PlayerProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "CreatedAt", "LastLoginAt", "PasswordHash", "Salt", "Username" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 10, 21, 4, 47, 12, 818, DateTimeKind.Utc).AddTicks(8761), null, "$2a$11$zlILsr8JDbaT3y5G6CyKdefDGx8WN5gD8dwLfztXD6y75fMqIu.LW", "70b1c7e5-5b6f-48f7-a9d4-76a6b9c34e68", "admin" });

            migrationBuilder.InsertData(
                table: "PlayerProfiles",
                columns: new[] { "ProfileId", "Avatar", "Bio", "DisplayName", "Draws", "EloRating", "Losses", "PlayerLevel", "PreferredSymbol", "TotalGamesPlayed", "UpdatedAt", "UserId", "Wins" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), null, "System Administrator", "Administrator", 0, 2000, 0, 99, null, 0, new DateTime(2025, 10, 21, 4, 47, 12, 818, DateTimeKind.Utc).AddTicks(8788), new Guid("00000000-0000-0000-0000-000000000001"), 0 });

            migrationBuilder.CreateIndex(
                name: "IX_GameHistories_Player1Id",
                table: "GameHistories",
                column: "Player1Id");

            migrationBuilder.CreateIndex(
                name: "IX_GameHistories_Player2Id",
                table: "GameHistories",
                column: "Player2Id");

            migrationBuilder.CreateIndex(
                name: "IX_GameHistories_RoomId",
                table: "GameHistories",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_GameHistories_StartTime",
                table: "GameHistories",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfiles_UserId",
                table: "PlayerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameHistories");

            migrationBuilder.DropTable(
                name: "PlayerProfiles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
