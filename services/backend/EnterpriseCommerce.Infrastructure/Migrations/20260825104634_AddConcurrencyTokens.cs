using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseCommerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "Version",
                table: "Orders",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                table: "InventoryItems",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "InventoryItems");
        }
    }
}
