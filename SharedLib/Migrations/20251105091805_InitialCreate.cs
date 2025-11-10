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
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "PlayerProfiles",
                columns: table => new
                {
                    ProfileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Elo = table.Column<int>(type: "int", nullable: false),
                    TotalGames = table.Column<int>(type: "int", nullable: false),
                    Wins = table.Column<int>(type: "int", nullable: false),
                    Losses = table.Column<int>(type: "int", nullable: false),
                    Draws = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Bio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsOnline = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastGameAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "Friendships",
                columns: table => new
                {
                    FriendshipId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    FriendId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friendships", x => x.FriendshipId);
                    table.CheckConstraint("CK_Friendship_NoSelfFriend", "[UserId] <> [FriendId]");
                    table.ForeignKey(
                        name: "FK_Friendships_PlayerProfiles_FriendId",
                        column: x => x.FriendId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Friendships_PlayerProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameHistories",
                columns: table => new
                {
                    GameId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Player1Id = table.Column<int>(type: "int", nullable: false),
                    Player2Id = table.Column<int>(type: "int", nullable: true),
                    IsAIGame = table.Column<bool>(type: "bit", nullable: false),
                    WinnerId = table.Column<int>(type: "int", nullable: true),
                    GameResult = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TotalMoves = table.Column<int>(type: "int", nullable: false),
                    GameDurationSeconds = table.Column<int>(type: "int", nullable: false),
                    PlayedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GameMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Player1EloChange = table.Column<int>(type: "int", nullable: false),
                    Player2EloChange = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameHistories", x => x.GameId);
                    table.ForeignKey(
                        name: "FK_GameHistories_PlayerProfiles_Player1Id",
                        column: x => x.Player1Id,
                        principalTable: "PlayerProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameHistories_PlayerProfiles_Player2Id",
                        column: x => x.Player2Id,
                        principalTable: "PlayerProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameHistories_PlayerProfiles_WinnerId",
                        column: x => x.WinnerId,
                        principalTable: "PlayerProfiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_FriendId",
                table: "Friendships",
                column: "FriendId");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_Status",
                table: "Friendships",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_UserId_FriendId",
                table: "Friendships",
                columns: new[] { "UserId", "FriendId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameHistories_PlayedAt",
                table: "GameHistories",
                column: "PlayedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GameHistories_Player1Id",
                table: "GameHistories",
                column: "Player1Id");

            migrationBuilder.CreateIndex(
                name: "IX_GameHistories_Player2Id",
                table: "GameHistories",
                column: "Player2Id");

            migrationBuilder.CreateIndex(
                name: "IX_GameHistories_WinnerId",
                table: "GameHistories",
                column: "WinnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfiles_Elo",
                table: "PlayerProfiles",
                column: "Elo");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfiles_PlayerName",
                table: "PlayerProfiles",
                column: "PlayerName");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfiles_UserId",
                table: "PlayerProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
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
                name: "Friendships");

            migrationBuilder.DropTable(
                name: "GameHistories");

            migrationBuilder.DropTable(
                name: "PlayerProfiles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
