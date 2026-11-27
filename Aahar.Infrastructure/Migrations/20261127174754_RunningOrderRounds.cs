using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aahar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RunningOrderRounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_OrderId_IngredientId_Kind",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_OrderItem_OrderId_MenuItemId",
                table: "OrderItem");

            migrationBuilder.DropIndex(
                name: "IX_KitchenTickets_OrderId",
                table: "KitchenTickets");

            migrationBuilder.AddColumn<int>(
                name: "KitchenTicketId",
                table: "StockMovements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "KitchenTickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_KitchenTicketId_IngredientId",
                table: "StockMovements",
                columns: new[] { "KitchenTicketId", "IngredientId" },
                unique: true,
                filter: "\"KitchenTicketId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrderId_IngredientId_Kind",
                table: "StockMovements",
                columns: new[] { "OrderId", "IngredientId", "Kind" },
                unique: true,
                filter: "\"OrderId\" IS NOT NULL AND \"KitchenTicketId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItem_OrderId_MenuItemId",
                table: "OrderItem",
                columns: new[] { "OrderId", "MenuItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTickets_OrderId_RequestId",
                table: "KitchenTickets",
                columns: new[] { "OrderId", "RequestId" },
                unique: true,
                filter: "\"RequestId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_KitchenTickets_KitchenTicketId",
                table: "StockMovements",
                column: "KitchenTicketId",
                principalTable: "KitchenTickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rolling back would discard round history. Restore a backup instead.");
        }
    }
}
