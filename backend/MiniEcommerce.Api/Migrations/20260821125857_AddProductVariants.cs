using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MiniEcommerce.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariants : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Order of operations (per ADR 0003 migration strategy):
        ///   1. Create ProductVariants table (empty at first; we fill it next).
        ///   2. Backfill one LEGACY variant per existing Product (Sku = "LEGACY-{ProductId}"),
        ///      copying the source-of-truth Stock from Products.Stock.
        ///   3. Add nullable ProductVariantId to CartItems and OrderItems.
        ///   4. Backfill CartItem.ProductVariantId and OrderItem.ProductVariantId by
        ///      joining to the matching LEGACY variant on ProductId. This MUST happen
        ///      before the FK to ProductVariants is added, otherwise the new FK would
        ///      fail on rows where ProductVariantId = 0.
        ///   5. Make ProductVariantId NOT NULL now that every row points at a real
        ///      variant.
        ///   6. Drop the old foreign keys to Products on CartItem/OrderItem.
        ///   7. Drop the old unique index IX_CartItems_CartId_ProductId.
        ///   8. Drop the old ProductId columns on CartItems and OrderItems (the model
        ///      no longer tracks them).
        ///   9. Add the new foreign keys to ProductVariants.
        ///  10. Add the new unique index IX_CartItems_CartId_ProductVariantId and the
        ///      supporting IX_*_ProductVariantId indexes.
        ///  11. Drop Products.Stock LAST — only safe after every cart/order row is
        ///      bound to a variant that already carries that stock.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create ProductVariants table.
            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    Size = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "text", nullable: true),
                    Stock = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductVariants_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // 2. Backfill one LEGACY variant per existing Product. The Sku
            // format and Stock copy come from ADR 0003 step 2 of the migration
            // strategy. The Size and Color are left NULL — they are attributes
            // that legacy products never had.
            migrationBuilder.Sql(
                @"INSERT INTO ""ProductVariants"" (""ProductId"", ""Sku"", ""Size"", ""Color"", ""Stock"", ""IsActive"", ""CreatedAt"")
                  SELECT ""Id"", 'LEGACY-' || ""Id"", NULL, NULL, ""Stock"", ""IsActive"", NOW()
                  FROM ""Products"";");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId",
                table: "ProductVariants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_Sku",
                table: "ProductVariants",
                column: "Sku",
                unique: true);

            // 3. Add nullable ProductVariantId columns. Nullable at first so
            // the FK added in step 9 has valid rows to point at.
            migrationBuilder.AddColumn<int>(
                name: "ProductVariantId",
                table: "OrderItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductVariantId",
                table: "CartItems",
                type: "integer",
                nullable: true);

            // 4. Backfill ProductVariantId by joining to the LEGACY variant on
            // ProductId. This must run before the new FK is created.
            migrationBuilder.Sql(
                @"UPDATE ""OrderItems"" oi
                  SET ""ProductVariantId"" = pv.""Id""
                  FROM ""ProductVariants"" pv
                  WHERE pv.""ProductId"" = oi.""ProductId""
                    AND pv.""Sku"" = 'LEGACY-' || oi.""ProductId"";");

            migrationBuilder.Sql(
                @"UPDATE ""CartItems"" ci
                  SET ""ProductVariantId"" = pv.""Id""
                  FROM ""ProductVariants"" pv
                  WHERE pv.""ProductId"" = ci.""ProductId""
                    AND pv.""Sku"" = 'LEGACY-' || ci.""ProductId"";");

            // 5. Now that every row has a valid variant, make ProductVariantId NOT NULL.
            migrationBuilder.AlterColumn<int>(
                name: "ProductVariantId",
                table: "OrderItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductVariantId",
                table: "CartItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // 6. Drop old foreign keys to Products on CartItem/OrderItem.
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_Products_ProductId",
                table: "CartItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems");

            // 7. Drop the old unique index on (CartId, ProductId).
            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_ProductId",
                table: "CartItems");

            // 8. Drop the old ProductId columns entirely. The model no longer
            // tracks them, so we don't re-add the FK to Products.
            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "OrderItems");

            // 9. Add the new foreign keys to ProductVariants.
            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_ProductVariants_ProductVariantId",
                table: "CartItems",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_ProductVariants_ProductVariantId",
                table: "OrderItems",
                column: "ProductVariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 10. New indexes — replace the unique (CartId, ProductId) with
            // (CartId, ProductVariantId) and add the supporting FK indexes.
            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductVariantId",
                table: "CartItems",
                columns: new[] { "CartId", "ProductVariantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ProductVariantId",
                table: "CartItems",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductVariantId",
                table: "OrderItems",
                column: "ProductVariantId");

            // 11. Drop Products.Stock LAST. Stock now lives on ProductVariants
            // (ADR 0003). Every LEGACY variant created in step 2 already
            // carries a copy of this value, so no data is lost.
            migrationBuilder.DropColumn(
                name: "Stock",
                table: "Products");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Down reverses the steps in reverse: re-create Products.Stock (sum
        /// from LEGACY variants), re-add ProductId columns to CartItems and
        /// OrderItems (nullable), backfill ProductId from the LEGACY variant's
        /// ProductId, swap the FKs and indexes back, drop ProductVariants.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore Products.Stock from the sum of LEGACY variant stocks for
            // each product. Non-LEGACY variants (created after the Up ran) are
            // ignored — their stock is lost on Down, which is acceptable for a
            // destructive rollback.
            migrationBuilder.AddColumn<int>(
                name: "Stock",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                @"UPDATE ""Products"" p
                  SET ""Stock"" = COALESCE((
                      SELECT SUM(""Stock"") FROM ""ProductVariants"" pv
                      WHERE pv.""ProductId"" = p.""Id"" AND pv.""Sku"" LIKE 'LEGACY-%'
                  ), 0);");

            // Drop new indexes.
            migrationBuilder.DropIndex(
                name: "IX_OrderItems_ProductVariantId",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_ProductVariantId",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_ProductVariantId",
                table: "CartItems");

            // Drop new FKs.
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_ProductVariants_ProductVariantId",
                table: "CartItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_ProductVariants_ProductVariantId",
                table: "OrderItems");

            // Re-add ProductId columns nullable so the FK can be re-attached.
            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "CartItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "OrderItems",
                type: "integer",
                nullable: true);

            // Backfill ProductId from the LEGACY variant's ProductId.
            migrationBuilder.Sql(
                @"UPDATE ""CartItems"" ci
                  SET ""ProductId"" = pv.""ProductId""
                  FROM ""ProductVariants"" pv
                  WHERE pv.""Id"" = ci.""ProductVariantId"";");

            migrationBuilder.Sql(
                @"UPDATE ""OrderItems"" oi
                  SET ""ProductId"" = pv.""ProductId""
                  FROM ""ProductVariants"" pv
                  WHERE pv.""Id"" = oi.""ProductVariantId"";");

            // Drop the new ProductVariantId columns.
            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ProductVariantId",
                table: "CartItems");

            // Make ProductId NOT NULL again.
            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "OrderItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ProductId",
                table: "CartItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // Re-add the old FK to Products.
            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_Products_ProductId",
                table: "CartItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Products_ProductId",
                table: "OrderItems",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Re-create the old unique index.
            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductId",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId" },
                unique: true);

            // Drop ProductVariants table (cascades the variant indexes).
            migrationBuilder.DropTable(
                name: "ProductVariants");
        }
    }
}
