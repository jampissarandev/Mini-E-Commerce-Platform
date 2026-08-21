using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniEcommerce.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressesUniqueDefaultIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Addresses_OneDefaultPerCustomer",
                table: "Addresses",
                column: "CustomerId",
                unique: true,
                filter: "\"IsDefault\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Addresses_OneDefaultPerCustomer",
                table: "Addresses");
        }
    }
}
