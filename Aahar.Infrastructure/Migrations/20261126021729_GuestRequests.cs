using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Aahar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuestRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TableId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AccessTokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    OrderId = table.Column<int>(type: "integer", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestRequests", x => x.Id);
                    table.CheckConstraint("CK_GuestRequests_Decision", "(\"Status\" = 'Pending' AND \"DecidedAt\" IS NULL AND \"DecidedBy\" IS NULL AND \"OrderId\" IS NULL AND \"RejectionReason\" IS NULL) OR (\"Status\" = 'Accepted' AND \"DecidedAt\" IS NOT NULL AND \"DecidedBy\" IS NOT NULL AND \"OrderId\" IS NOT NULL AND \"RejectionReason\" IS NULL) OR (\"Status\" = 'Rejected' AND \"DecidedAt\" IS NOT NULL AND \"DecidedBy\" IS NOT NULL AND \"OrderId\" IS NULL AND \"RejectionReason\" IS NOT NULL)");
                    table.CheckConstraint("CK_GuestRequests_Token", "octet_length(\"AccessTokenHash\") = 32");
                    table.ForeignKey(
                        name: "FK_GuestRequests_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestRequests_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuestRequestItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuestRequestId = table.Column<int>(type: "integer", nullable: false),
                    MenuItemId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    QuotedUnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestRequestItem", x => x.Id);
                    table.CheckConstraint("CK_GuestRequestItems_Values", "\"Quantity\" > 0 AND \"QuotedUnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_GuestRequestItem_GuestRequests_GuestRequestId",
                        column: x => x.GuestRequestId,
                        principalTable: "GuestRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestRequestItem_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestRequestItem_GuestRequestId_MenuItemId",
                table: "GuestRequestItem",
                columns: new[] { "GuestRequestId", "MenuItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestRequestItem_MenuItemId",
                table: "GuestRequestItem",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestRequests_OrderId",
                table: "GuestRequests",
                column: "OrderId",
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GuestRequests_TableId",
                table: "GuestRequests",
                column: "TableId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Rolling back would destroy guest request history. Restore a backup instead.");
        }
    }
}
