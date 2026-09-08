using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DGVisionStudio.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260908224000_AddPricingItems")]
    public partial class AddPricingItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    PricingMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PriceText = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PricingItems_DisplayOrder",
                table: "PricingItems",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_PricingItems_IsActive",
                table: "PricingItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PricingItems_PricingMode",
                table: "PricingItems",
                column: "PricingMode");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PricingItems");
        }
    }
}
