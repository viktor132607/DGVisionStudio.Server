using System;
using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DGVisionStudio.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260728130000_AddPortfolioAlbumPublishAtUtc")]
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
