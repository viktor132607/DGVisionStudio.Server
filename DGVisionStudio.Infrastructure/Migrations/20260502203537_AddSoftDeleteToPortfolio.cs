using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DGVisionStudio.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToPortfolio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "PortfolioImages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PortfolioImages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "PortfolioCategories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PortfolioCategories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "PortfolioAlbums",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PortfolioAlbums",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdminUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AdminEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioImages_DeletedAtUtc",
                table: "PortfolioImages",
                column: "DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioImages_IsDeleted",
                table: "PortfolioImages",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioCategories_DeletedAtUtc",
                table: "PortfolioCategories",
                column: "DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioCategories_IsDeleted",
                table: "PortfolioCategories",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioAlbums_DeletedAtUtc",
                table: "PortfolioAlbums",
                column: "DeletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioAlbums_IsDeleted",
                table: "PortfolioAlbums",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_AdminEmail",
                table: "AuditLogs",
                column: "AdminEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_AdminUserId",
                table: "AuditLogs",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAtUtc",
                table: "AuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityId",
                table: "AuditLogs",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType",
                table: "AuditLogs",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioImages_DeletedAtUtc",
                table: "PortfolioImages");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioImages_IsDeleted",
                table: "PortfolioImages");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioCategories_DeletedAtUtc",
                table: "PortfolioCategories");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioCategories_IsDeleted",
                table: "PortfolioCategories");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioAlbums_DeletedAtUtc",
                table: "PortfolioAlbums");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioAlbums_IsDeleted",
                table: "PortfolioAlbums");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "PortfolioImages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PortfolioImages");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "PortfolioCategories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PortfolioCategories");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "PortfolioAlbums");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PortfolioAlbums");
        }
    }
}
