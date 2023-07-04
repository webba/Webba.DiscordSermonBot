using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Webba.DiscordSermonBot.Backend.Migrations
{
    /// <inheritdoc />
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable IDE1006
    public partial class initial : Migration
#pragma warning restore IDE1006
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rotations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GuildId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ScheduledMessageId = table.Column<long>(type: "bigint", nullable: true),
                    ScheduledMessageTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScheduledMessageCharacter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastSermonTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RotationId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CharacterName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    LastFatihTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextSermonTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SermonRotationId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Members_Rotations_RotationId",
                        column: x => x.RotationId,
                        principalTable: "Rotations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Members_Rotations_SermonRotationId",
                        column: x => x.SermonRotationId,
                        principalTable: "Rotations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Members_RotationId",
                table: "Members",
                column: "RotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_SermonRotationId",
                table: "Members",
                column: "SermonRotationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "Rotations");
        }
    }
}
