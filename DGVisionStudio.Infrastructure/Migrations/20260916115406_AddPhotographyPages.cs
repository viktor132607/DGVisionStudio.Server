using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DGVisionStudio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotographyPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing calendar, pricing and portfolio columns have earlier migrations/schema setup.
            // Only create the new service-page table here; never recreate existing production tables.

            migrationBuilder.CreateTable(
                name: "PhotographyPages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TitleEn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DescriptionEn = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    BodyEn = table.Column<string>(type: "text", nullable: false),
                    Preparation = table.Column<string>(type: "text", nullable: false),
                    PreparationEn = table.Column<string>(type: "text", nullable: false),
                    PortfolioCategoryId = table.Column<int>(type: "integer", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotographyPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotographyPages_PortfolioCategories_PortfolioCategoryId",
                        column: x => x.PortfolioCategoryId,
                        principalTable: "PortfolioCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhotographyPages_PortfolioCategoryId",
                table: "PhotographyPages",
                column: "PortfolioCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotographyPages_Slug",
                table: "PhotographyPages",
                column: "Slug",
                unique: true,
                filter: "\"IsDeleted\" = false");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropTable(
                name: "PhotographyPages");

        }
    }
}
