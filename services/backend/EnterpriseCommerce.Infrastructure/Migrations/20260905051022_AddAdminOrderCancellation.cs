using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseCommerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminOrderCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminOrderCancellations",
                columns: table => new
                {
                    OrderId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ActorIssuer = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false, collation: "ascii_bin"),
                    ActorSubject = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false, collation: "ascii_bin"),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminOrderCancellations", x => x.OrderId);
                    table.ForeignKey(
                        name: "FK_AdminOrderCancellations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminOrderCancellations");
        }
    }
}
