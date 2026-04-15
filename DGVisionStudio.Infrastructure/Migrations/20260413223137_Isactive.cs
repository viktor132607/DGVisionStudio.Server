using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DGVisionStudio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Isactive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PortfolioAlbums",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PortfolioAlbums");
        }
    }
}
