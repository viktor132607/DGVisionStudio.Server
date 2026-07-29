using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DGVisionStudio.Infrastructure.Migrations
{
    public partial class AddPortfolioAlbumPublishAtUtc : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublishAtUtc",
                table: "PortfolioAlbums",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioAlbums_PublishAtUtc",
                table: "PortfolioAlbums",
                column: "PublishAtUtc");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortfolioAlbums_PublishAtUtc",
                table: "PortfolioAlbums");

            migrationBuilder.DropColumn(
                name: "PublishAtUtc",
                table: "PortfolioAlbums");
        }
    }
}
