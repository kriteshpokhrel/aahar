using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aahar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StockLedgerAndTableOccupancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Orders"
                        WHERE "Status" IN ('Draft', 'Sent', 'Completed')
                        GROUP BY "TableId" HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot add table occupancy constraint: multiple active orders share a table';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_Orders_TableId",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Orders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationStockDisposition",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledBy",
                table: "Orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConsumptionMovementCount",
                table: "Orders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    QuantityChange = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    Actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.CheckConstraint("CK_StockMovements_Balance", "\"BalanceAfter\" >= 0");
                    table.CheckConstraint("CK_StockMovements_Kind", "(\"Kind\" = 'Opening' AND \"QuantityChange\" >= 0 AND \"OrderId\" IS NULL) OR (\"Kind\" = 'Receipt' AND \"QuantityChange\" > 0 AND \"OrderId\" IS NULL) OR (\"Kind\" = 'Consumption' AND \"QuantityChange\" < 0 AND \"OrderId\" IS NOT NULL) OR (\"Kind\" = 'Cancellation' AND \"QuantityChange\" > 0 AND \"OrderId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_StockMovements_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO "StockMovements" ("IngredientId", "Kind", "QuantityChange", "BalanceAfter", "Actor", "OccurredAt")
                SELECT "Id", 'Opening', "Stock", "Stock", 'system:migration', CURRENT_TIMESTAMP
                FROM "Ingredients";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ActiveTable",
                table: "Orders",
                column: "TableId",
                unique: true,
                filter: "\"Status\" IN ('Draft', 'Sent', 'Completed')");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_IngredientId_Id",
                table: "StockMovements",
                columns: new[] { "IngredientId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_OrderId_IngredientId_Kind",
                table: "StockMovements",
                columns: new[] { "OrderId", "IngredientId", "Kind" },
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rolling back would destroy stock history. Restore a backup instead.");
        }
    }
}
