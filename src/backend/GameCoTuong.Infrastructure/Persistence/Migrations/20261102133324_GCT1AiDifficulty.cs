using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameCoTuong.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GCT1AiDifficulty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiDifficulty",
                table: "Matches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "medium");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiDifficulty",
                table: "Matches");
        }
    }
}
