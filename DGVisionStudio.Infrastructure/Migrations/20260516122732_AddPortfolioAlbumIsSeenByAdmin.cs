using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DGVisionStudio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioAlbumIsSeenByAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSeenByAdmin",
                table: "PortfolioAlbums",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsSeenByAdmin",
                table: "ContactRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "IsSeenByAdmin",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioAlbums_IsSeenByAdmin",
                table: "PortfolioAlbums",
                column: "IsSeenByAdmin");

            migrationBuilder.CreateIndex(
                name: "IX_ContactRequests_IsSeenByAdmin",
                table: "ContactRequests",
                column: "IsSeenByAdmin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PortfolioAlbums_IsSeenByAdmin",
                table: "PortfolioAlbums");

            migrationBuilder.DropIndex(
                name: "IX_ContactRequests_IsSeenByAdmin",
                table: "ContactRequests");

            migrationBuilder.DropColumn(
                name: "IsSeenByAdmin",
                table: "PortfolioAlbums");

            migrationBuilder.AlterColumn<bool>(
                name: "IsSeenByAdmin",
                table: "ContactRequests",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsSeenByAdmin",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");
        }
    }
}
