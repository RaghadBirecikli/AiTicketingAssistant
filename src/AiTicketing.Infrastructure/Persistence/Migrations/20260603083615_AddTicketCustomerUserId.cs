using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTicketing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketCustomerUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerUserId",
                table: "Tickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CustomerUserId",
                table: "Tickets",
                column: "CustomerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_CustomerUserId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "CustomerUserId",
                table: "Tickets");
        }
    }
}
