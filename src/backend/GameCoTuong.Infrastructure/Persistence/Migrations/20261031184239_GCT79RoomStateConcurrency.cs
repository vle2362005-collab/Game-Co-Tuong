using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameCoTuong.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GCT79RoomStateConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_RoomId",
                table: "Matches");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_RoomId",
                table: "Matches",
                column: "RoomId",
                unique: true,
                filter: "[Status] IN ('playing', 'paused')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Matches_RoomId",
                table: "Matches");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_RoomId",
                table: "Matches",
                column: "RoomId");
        }
    }
}
