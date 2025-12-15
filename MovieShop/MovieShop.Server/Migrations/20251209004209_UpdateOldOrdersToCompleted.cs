using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieShop.Server.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOldOrdersToCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update old orders with Pending/Processing/Shipped/Delivered status to Completed
            // Since we switched to digital streaming, all non-cancelled orders should be Completed
            migrationBuilder.Sql(@"
                UPDATE Orders 
                SET Status = 'Completed' 
                WHERE Status IN ('Pending', 'Processing', 'Shipped', 'Delivered')
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
