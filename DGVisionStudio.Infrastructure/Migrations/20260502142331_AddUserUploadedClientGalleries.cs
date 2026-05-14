using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DGVisionStudio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserUploadedClientGalleries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "PortfolioAlbums",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUserUploaded",
                table: "PortfolioAlbums",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "PortfolioAlbums",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserGalleryStatus",
                table: "PortfolioAlbums",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioAlbums_ExpiresAtUtc",
                table: "PortfolioAlbums",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioAlbums_IsUserUploaded",
                table: "PortfolioAlbums",
                column: "IsUserUploaded");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioAlbums_OwnerUserId",
                table: "PortfolioAlbums",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioAlbums_UserGalleryStatus",
                table: "PortfolioAlbums",
                column: "UserGalleryStatus");

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioAlbums_AspNetUsers_OwnerUserId",
                table: "PortfolioAlbums",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioAlbums_AspNetUsers_OwnerUserId",
                table: "PortfolioAlbums");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioAlbums_ExpiresAtUtc",
                table: "PortfolioAlbums");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioAlbums_IsUserUploaded",
                table: "PortfolioAlbums");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioAlbums_OwnerUserId",
                table: "PortfolioAlbums");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioAlbums_UserGalleryStatus",
                table: "PortfolioAlbums");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "PortfolioAlbums");

            migrationBuilder.DropColumn(
                name: "IsUserUploaded",
                table: "PortfolioAlbums");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "PortfolioAlbums");

            migrationBuilder.DropColumn(
                name: "UserGalleryStatus",
                table: "PortfolioAlbums");
        }
    }
}
