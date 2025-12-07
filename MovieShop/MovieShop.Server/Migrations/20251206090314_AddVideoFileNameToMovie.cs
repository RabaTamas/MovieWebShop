using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieShop.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoFileNameToMovie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VideoFileName",
                table: "Movies",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoFileName",
                table: "Movies");
        }
    }
}
