using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aahar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoundLedgerConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_Ticket",
                table: "StockMovements",
                sql: "\"KitchenTicketId\" IS NULL OR (\"Kind\" = 'Consumption' AND \"OrderId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTickets_OrderId",
                table: "KitchenTickets",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_Ticket",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_KitchenTickets_OrderId",
                table: "KitchenTickets");
        }
    }
}
