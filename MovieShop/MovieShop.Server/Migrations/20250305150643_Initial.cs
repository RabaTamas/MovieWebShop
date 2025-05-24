using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MovieShop.Server.Migrations
{
    public class _20250305150643_Initial
    {
        /// <inheritdoc />
        public partial class Initial : Migration
        {
            /// <inheritdoc />
            protected override void Up(MigrationBuilder migrationBuilder)
            {
                migrationBuilder.CreateTable(
                    name: "Categories",
                    columns: table => new
                    {
                        Id = table.Column<int>(type: "int", nullable: false)
                            .Annotation("SqlServer:Identity", "1, 1"),
                        Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Categories", x => x.Id);
                    });

                migrationBuilder.CreateTable(
                    name: "Movies",
                    columns: table => new
                    {
                        Id = table.Column<int>(type: "int", nullable: false)
                            .Annotation("SqlServer:Identity", "1, 1"),
                        Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                        Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                        ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                        Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                        DiscountedPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Movies", x => x.Id);
                    });

                migrationBuilder.CreateTable(
                    name: "Users",
                    columns: table => new
                    {
                        Id = table.Column<int>(type: "int", nullable: false)
                            .Annotation("SqlServer:Identity", "1, 1"),
                        Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                        Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                        PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Users", x => x.Id);
                    });

                migrationBuilder.CreateTable(
                    name: "MovieCategory",
                    columns: table => new
                    {
                        CategoryId = table.Column<int>(type: "int", nullable: false),
                        MovieId = table.Column<int>(type: "int", nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_MovieCategory", x => new { x.CategoryId, x.MovieId });
                        table.ForeignKey(
                            name: "FK_MovieCategory_Categories_CategoryId",
                            column: x => x.CategoryId,
                            principalTable: "Categories",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Restrict);
                        table.ForeignKey(
                            name: "FK_MovieCategory_Movies_MovieId",
                            column: x => x.MovieId,
                            principalTable: "Movies",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Restrict);
                    });

                migrationBuilder.CreateTable(
                    name: "Addresses",
                    columns: table => new
                    {
                        Id = table.Column<int>(type: "int", nullable: false)
                            .Annotation("SqlServer:Identity", "1, 1"),
                        Street = table.Column<string>(type: "nvarchar(max)", nullable: false),
                        City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                        Zip = table.Column<string>(type: "nvarchar(max)", nullable: false),
                        UserId = table.Column<int>(type: "int", nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Addresses", x => x.Id);
                        table.ForeignKey(
                            name: "FK_Addresses_Users_UserId",
                            column: x => x.UserId,
                            principalTable: "Users",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);
                    });

                migrationBuilder.CreateTable(
                    name: "Reviews",
                    columns: table => new
                    {
                        Id = table.Column<int>(type: "int", nullable: false)
                            .Annotation("SqlServer:Identity", "1, 1"),
                        Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                        UserId = table.Column<int>(type: "int", nullable: false),
                        MovieId = table.Column<int>(type: "int", nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Reviews", x => x.Id);
                        table.ForeignKey(
                            name: "FK_Reviews_Movies_MovieId",
                            column: x => x.MovieId,
                            principalTable: "Movies",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);
                        table.ForeignKey(
                            name: "FK_Reviews_Users_UserId",
                            column: x => x.UserId,
                            principalTable: "Users",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);
                    });

                migrationBuilder.CreateTable(
                    name: "ShoppingCarts",
                    columns: table => new
                    {
                        Id = table.Column<int>(type: "int", nullable: false)
                            .Annotation("SqlServer:Identity", "1, 1"),
                        UserId = table.Column<int>(type: "int", nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_ShoppingCarts", x => x.Id);
                        table.ForeignKey(
                            name: "FK_ShoppingCarts_Users_UserId",
                            column: x => x.UserId,
                            principalTable: "Users",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);
                    });

                migrationBuilder.CreateTable(
                    name: "Orders",
                    columns: table => new
                    {
                        Id = table.Column<int>(type: "int", nullable: false)
                            .Annotation("SqlServer:Identity", "1, 1"),
                        OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                        TotalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                        UserId = table.Column<int>(type: "int", nullable: false),
                        BillingAddressId = table.Column<int>(type: "int", nullable: false),
                        ShippingAddressId = table.Column<int>(type: "int", nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_Orders", x => x.Id);
                        table.ForeignKey(
                            name: "FK_Orders_Addresses_BillingAddressId",
                            column: x => x.BillingAddressId,
                            principalTable: "Addresses",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Restrict);
                        table.ForeignKey(
                            name: "FK_Orders_Addresses_ShippingAddressId",
                            column: x => x.ShippingAddressId,
                            principalTable: "Addresses",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Restrict);
                        table.ForeignKey(
                            name: "FK_Orders_Users_UserId",
                            column: x => x.UserId,
                            principalTable: "Users",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);
                    });

                migrationBuilder.CreateTable(
                    name: "ShoppingCartMovies",
                    columns: table => new
                    {
                        ShoppingCartId = table.Column<int>(type: "int", nullable: false),
                        MovieId = table.Column<int>(type: "int", nullable: false),
                        Quantity = table.Column<int>(type: "int", nullable: false),
                        PriceAtOrder = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_ShoppingCartMovies", x => new { x.ShoppingCartId, x.MovieId });
                        table.ForeignKey(
                            name: "FK_ShoppingCartMovies_Movies_MovieId",
                            column: x => x.MovieId,
                            principalTable: "Movies",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);
                        table.ForeignKey(
                            name: "FK_ShoppingCartMovies_ShoppingCarts_ShoppingCartId",
                            column: x => x.ShoppingCartId,
                            principalTable: "ShoppingCarts",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);
                    });

                migrationBuilder.CreateTable(
                    name: "OrderMovies",
                    columns: table => new
                    {
                        OrderId = table.Column<int>(type: "int", nullable: false),
                        MovieId = table.Column<int>(type: "int", nullable: false),
                        Quantity = table.Column<int>(type: "int", nullable: false),
                        PriceAtOrder = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_OrderMovies", x => new { x.OrderId, x.MovieId });
                        table.ForeignKey(
                            name: "FK_OrderMovies_Movies_MovieId",
                            column: x => x.MovieId,
                            principalTable: "Movies",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Restrict);
                        table.ForeignKey(
                            name: "FK_OrderMovies_Orders_OrderId",
                            column: x => x.OrderId,
                            principalTable: "Orders",
                            principalColumn: "Id",
                            onDelete: ReferentialAction.Cascade);
                    });

                migrationBuilder.CreateIndex(
                    name: "IX_Addresses_UserId",
                    table: "Addresses",
                    column: "UserId");

                migrationBuilder.CreateIndex(
                    name: "IX_MovieCategory_MovieId",
                    table: "MovieCategory",
                    column: "MovieId");

                migrationBuilder.CreateIndex(
                    name: "IX_OrderMovies_MovieId",
                    table: "OrderMovies",
                    column: "MovieId");

                migrationBuilder.CreateIndex(
                    name: "IX_Orders_BillingAddressId",
                    table: "Orders",
                    column: "BillingAddressId");

                migrationBuilder.CreateIndex(
                    name: "IX_Orders_ShippingAddressId",
                    table: "Orders",
                    column: "ShippingAddressId");

                migrationBuilder.CreateIndex(
                    name: "IX_Orders_UserId",
                    table: "Orders",
                    column: "UserId");

                migrationBuilder.CreateIndex(
                    name: "IX_Reviews_MovieId",
                    table: "Reviews",
                    column: "MovieId");

                migrationBuilder.CreateIndex(
                    name: "IX_Reviews_UserId",
                    table: "Reviews",
                    column: "UserId");

                migrationBuilder.CreateIndex(
                    name: "IX_ShoppingCartMovies_MovieId",
                    table: "ShoppingCartMovies",
                    column: "MovieId");

                migrationBuilder.CreateIndex(
                    name: "IX_ShoppingCarts_UserId",
                    table: "ShoppingCarts",
                    column: "UserId",
                    unique: true);
            }

            /// <inheritdoc />
            protected override void Down(MigrationBuilder migrationBuilder)
            {
                migrationBuilder.DropTable(
                    name: "MovieCategory");

                migrationBuilder.DropTable(
                    name: "OrderMovies");

                migrationBuilder.DropTable(
                    name: "Reviews");

                migrationBuilder.DropTable(
                    name: "ShoppingCartMovies");

                migrationBuilder.DropTable(
                    name: "Categories");

                migrationBuilder.DropTable(
                    name: "Orders");

                migrationBuilder.DropTable(
                    name: "Movies");

                migrationBuilder.DropTable(
                    name: "ShoppingCarts");

                migrationBuilder.DropTable(
                    name: "Addresses");

                migrationBuilder.DropTable(
                    name: "Users");
            }
        }
    }
}
