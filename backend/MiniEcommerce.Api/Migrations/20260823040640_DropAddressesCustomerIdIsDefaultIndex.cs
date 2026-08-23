using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniEcommerce.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropAddressesCustomerIdIsDefaultIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Addresses_CustomerId_IsDefault",
                table: "Addresses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Addresses_CustomerId_IsDefault",
                table: "Addresses",
                columns: new[] { "CustomerId", "IsDefault" });
        }
    }
}
