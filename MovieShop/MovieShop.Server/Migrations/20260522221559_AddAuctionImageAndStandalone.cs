using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieShop.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionImageAndStandalone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Auctions_Movies_MovieId",
                table: "Auctions");

            migrationBuilder.AlterColumn<int>(
                name: "MovieId",
                table: "Auctions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "AuctionTitle",
                table: "Auctions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Auctions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Auctions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Auctions_Movies_MovieId",
                table: "Auctions",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Auctions_Movies_MovieId",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "AuctionTitle",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Auctions");

            migrationBuilder.AlterColumn<int>(
                name: "MovieId",
                table: "Auctions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Auctions_Movies_MovieId",
                table: "Auctions",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
