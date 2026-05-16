using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DGVisionStudio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGalleryTypeToPortfolioAlbums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GalleryType",
                table: "PortfolioAlbums",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioAlbums_GalleryType",
                table: "PortfolioAlbums",
                column: "GalleryType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortfolioAlbums_GalleryType",
                table: "PortfolioAlbums");

            migrationBuilder.DropColumn(
                name: "GalleryType",
                table: "PortfolioAlbums");
        }
    }
}
