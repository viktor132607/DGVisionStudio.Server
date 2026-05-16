using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DGVisionStudio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintRequestsAndAdminNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSeenByAdmin",
                table: "ContactRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsSeenByAdmin",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PrintRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    PortfolioAlbumId = table.Column<int>(type: "integer", nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "New"),
                    IsSeenByAdmin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrintRequests_PortfolioAlbums_PortfolioAlbumId",
                        column: x => x.PortfolioAlbumId,
                        principalTable: "PortfolioAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrintRequestItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PrintRequestId = table.Column<int>(type: "integer", nullable: false),
                    PortfolioImageId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Size = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PaperType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintRequestItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintRequestItems_PortfolioImages_PortfolioImageId",
                        column: x => x.PortfolioImageId,
                        principalTable: "PortfolioImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrintRequestItems_PrintRequests_PrintRequestId",
                        column: x => x.PrintRequestId,
                        principalTable: "PrintRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequestItems_PortfolioImageId",
                table: "PrintRequestItems",
                column: "PortfolioImageId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequestItems_PrintRequestId",
                table: "PrintRequestItems",
                column: "PrintRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequests_CreatedAtUtc",
                table: "PrintRequests",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequests_IsSeenByAdmin",
                table: "PrintRequests",
                column: "IsSeenByAdmin");

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequests_PortfolioAlbumId",
                table: "PrintRequests",
                column: "PortfolioAlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequests_Status",
                table: "PrintRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PrintRequests_UserId",
                table: "PrintRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrintRequestItems");

            migrationBuilder.DropTable(
                name: "PrintRequests");

            migrationBuilder.DropColumn(
                name: "IsSeenByAdmin",
                table: "ContactRequests");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsSeenByAdmin",
                table: "AspNetUsers");
        }
    }
}
