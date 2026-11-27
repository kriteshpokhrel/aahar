using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aahar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TakeawayAndKitchenTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Orders" o
                        WHERE o."Status" = 'Sent'
                          AND NOT EXISTS (SELECT 1 FROM "OrderItem" i WHERE i."OrderId" = o."Id")
                    ) THEN
                        RAISE EXCEPTION 'Cannot reconstruct a kitchen ticket for a sent order without items';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_Orders_ActiveTable",
                table: "Orders");

            migrationBuilder.AlterColumn<int>(
                name: "TableId",
                table: "Orders",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "Orders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "OrderItem",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KitchenTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsReconstructed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenTickets", x => x.Id);
                    table.CheckConstraint("CK_KitchenTickets_Status", "\"Status\" IN ('Queued', 'Preparing', 'Ready', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_KitchenTickets_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KitchenTicketItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KitchenTicketId = table.Column<int>(type: "integer", nullable: false),
                    MenuItemId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenTicketItem", x => x.Id);
                    table.CheckConstraint("CK_KitchenTicketItems_Values", "\"Quantity\" > 0 AND \"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_KitchenTicketItem_KitchenTickets_KitchenTicketId",
                        column: x => x.KitchenTicketId,
                        principalTable: "KitchenTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ActiveTable",
                table: "Orders",
                column: "TableId",
                unique: true,
                filter: "\"TableId\" IS NOT NULL AND \"Status\" IN ('Draft', 'Sent', 'Completed')");

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTicketItem_KitchenTicketId_MenuItemId",
                table: "KitchenTicketItem",
                columns: new[] { "KitchenTicketId", "MenuItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KitchenTickets_OrderId",
                table: "KitchenTickets",
                column: "OrderId",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "KitchenTickets" ("OrderId", "Status", "CreatedAt", "IsReconstructed")
                SELECT "Id", 'Queued', "CreatedAt", TRUE FROM "Orders" WHERE "Status" = 'Sent';

                INSERT INTO "KitchenTicketItem" ("KitchenTicketId", "MenuItemId", "Name", "Quantity", "UnitPrice", "Notes")
                SELECT t."Id", i."MenuItemId", i."Name", i."Quantity", i."UnitPrice", i."Notes"
                FROM "KitchenTickets" t JOIN "OrderItem" i ON i."OrderId" = t."OrderId"
                WHERE t."IsReconstructed";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rolling back would destroy kitchen history and takeaway orders. Restore a backup instead.");
        }
    }
}
