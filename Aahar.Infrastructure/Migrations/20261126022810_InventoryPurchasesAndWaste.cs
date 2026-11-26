using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aahar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InventoryPurchasesAndWaste : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_Kind",
                table: "StockMovements");

            migrationBuilder.AddColumn<int>(
                name: "PurchaseReceiptLineId",
                table: "StockMovements",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "StockMovements",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LowStockThreshold",
                table: "Ingredients",
                type: "numeric(14,3)",
                precision: 14,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "IngredientThresholdChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    PreviousThreshold = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: true),
                    NewThreshold = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    Actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientThresholdChanges", x => x.Id);
                    table.CheckConstraint("CK_ThresholdChanges_Values", "\"NewThreshold\" >= 0 AND (\"PreviousThreshold\" IS NULL OR \"PreviousThreshold\" >= 0)");
                    table.ForeignKey(
                        name: "FK_IngredientThresholdChanges_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO "IngredientThresholdChanges" ("IngredientId", "PreviousThreshold", "NewThreshold", "Actor", "Reason", "OccurredAt")
                SELECT "Id", NULL, "LowStockThreshold", 'system:migration', 'Initial threshold', CURRENT_TIMESTAMP
                FROM "Ingredients";
                """);

            migrationBuilder.CreateTable(
                name: "PurchaseReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceipts", x => x.Id);
                    table.CheckConstraint("CK_PurchaseReceipts_Total", "\"Total\" >= 0");
                    table.ForeignKey(
                        name: "FK_PurchaseReceipts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierChanges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SupplierId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierChanges_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseReceiptLine",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PurchaseReceiptId = table.Column<int>(type: "integer", nullable: false),
                    IngredientId = table.Column<int>(type: "integer", nullable: false),
                    IngredientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceiptLine", x => x.Id);
                    table.CheckConstraint("CK_PurchaseReceiptLines_Values", "\"Quantity\" > 0 AND \"UnitCost\" >= 0 AND \"LineTotal\" >= 0");
                    table.ForeignKey(
                        name: "FK_PurchaseReceiptLine_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseReceiptLine_PurchaseReceipts_PurchaseReceiptId",
                        column: x => x.PurchaseReceiptId,
                        principalTable: "PurchaseReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PurchaseReceiptLineId",
                table: "StockMovements",
                column: "PurchaseReceiptLineId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_Kind",
                table: "StockMovements",
                sql: "((\"Kind\" = 'Opening' AND \"QuantityChange\" >= 0 AND \"OrderId\" IS NULL) OR (\"Kind\" = 'Receipt' AND \"QuantityChange\" > 0 AND \"OrderId\" IS NULL) OR (\"Kind\" = 'Consumption' AND \"QuantityChange\" < 0 AND \"OrderId\" IS NOT NULL) OR (\"Kind\" = 'Cancellation' AND \"QuantityChange\" > 0 AND \"OrderId\" IS NOT NULL) OR (\"Kind\" = 'Waste' AND \"QuantityChange\" < 0 AND \"OrderId\" IS NULL)) AND \"PurchaseReceiptLineId\" IS NULL OR (\"Kind\" = 'Purchase' AND \"QuantityChange\" > 0 AND \"OrderId\" IS NULL AND \"PurchaseReceiptLineId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_Reason",
                table: "StockMovements",
                sql: "\"Kind\" NOT IN ('Purchase', 'Waste') OR (\"Reason\" IS NOT NULL AND length(btrim(\"Reason\")) > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Ingredients_Threshold",
                table: "Ingredients",
                sql: "\"LowStockThreshold\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientThresholdChanges_IngredientId_Id",
                table: "IngredientThresholdChanges",
                columns: new[] { "IngredientId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceiptLine_IngredientId",
                table: "PurchaseReceiptLine",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceiptLine_PurchaseReceiptId_IngredientId",
                table: "PurchaseReceiptLine",
                columns: new[] { "PurchaseReceiptId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_SupplierId_Reference",
                table: "PurchaseReceipts",
                columns: new[] { "SupplierId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierChanges_SupplierId_Id",
                table: "SupplierChanges",
                columns: new[] { "SupplierId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_PurchaseReceiptLine_PurchaseReceiptLineId",
                table: "StockMovements",
                column: "PurchaseReceiptLineId",
                principalTable: "PurchaseReceiptLine",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rolling back would destroy purchase and inventory history. Restore a backup instead.");
        }
    }
}
