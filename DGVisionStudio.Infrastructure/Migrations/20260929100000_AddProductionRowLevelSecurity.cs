using DGVisionStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DGVisionStudio.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260929100000_AddProductionRowLevelSecurity")]
public partial class AddProductionRowLevelSecurity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "PortfolioAlbums" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE "PortfolioAlbums" FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS "PortfolioAlbums_Select" ON "PortfolioAlbums";
            DROP POLICY IF EXISTS "PortfolioAlbums_Insert" ON "PortfolioAlbums";
            DROP POLICY IF EXISTS "PortfolioAlbums_Update" ON "PortfolioAlbums";
            DROP POLICY IF EXISTS "PortfolioAlbums_Delete" ON "PortfolioAlbums";

            CREATE POLICY "PortfolioAlbums_Select"
            ON "PortfolioAlbums"
            FOR SELECT
            USING (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR (
                    "OwnerUserId" IS NOT NULL
                    AND "OwnerUserId" = current_setting('app.current_user_id', true)
                )
                OR (
                    NOT "IsUserUploaded"
                    AND "IsPublished"
                    AND NOT "IsDeleted"
                )
                OR EXISTS (
                    SELECT 1
                    FROM "UserAlbumAccesses" access
                    WHERE access."PortfolioAlbumId" = "PortfolioAlbums"."Id"
                      AND access."UserId" = current_setting('app.current_user_id', true)
                      AND access."PreviewEnabled"
                )
            );

            CREATE POLICY "PortfolioAlbums_Insert"
            ON "PortfolioAlbums"
            FOR INSERT
            WITH CHECK (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR (
                    "OwnerUserId" IS NOT NULL
                    AND "OwnerUserId" = current_setting('app.current_user_id', true)
                )
            );

            CREATE POLICY "PortfolioAlbums_Update"
            ON "PortfolioAlbums"
            FOR UPDATE
            USING (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR (
                    "OwnerUserId" IS NOT NULL
                    AND "OwnerUserId" = current_setting('app.current_user_id', true)
                )
            )
            WITH CHECK (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR (
                    "OwnerUserId" IS NOT NULL
                    AND "OwnerUserId" = current_setting('app.current_user_id', true)
                )
            );

            CREATE POLICY "PortfolioAlbums_Delete"
            ON "PortfolioAlbums"
            FOR DELETE
            USING (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR (
                    "OwnerUserId" IS NOT NULL
                    AND "OwnerUserId" = current_setting('app.current_user_id', true)
                )
            );

            ALTER TABLE "PortfolioImages" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE "PortfolioImages" FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS "PortfolioImages_Select" ON "PortfolioImages";
            DROP POLICY IF EXISTS "PortfolioImages_Insert" ON "PortfolioImages";
            DROP POLICY IF EXISTS "PortfolioImages_Update" ON "PortfolioImages";
            DROP POLICY IF EXISTS "PortfolioImages_Delete" ON "PortfolioImages";

            CREATE POLICY "PortfolioImages_Select"
            ON "PortfolioImages"
            FOR SELECT
            USING (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR EXISTS (
                    SELECT 1
                    FROM "PortfolioAlbums" album
                    WHERE album."Id" = "PortfolioImages"."PortfolioAlbumId"
                )
            );

            CREATE POLICY "PortfolioImages_Insert"
            ON "PortfolioImages"
            FOR INSERT
            WITH CHECK (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR EXISTS (
                    SELECT 1
                    FROM "PortfolioAlbums" album
                    WHERE album."Id" = "PortfolioImages"."PortfolioAlbumId"
                      AND album."OwnerUserId" = current_setting('app.current_user_id', true)
                )
            );

            CREATE POLICY "PortfolioImages_Update"
            ON "PortfolioImages"
            FOR UPDATE
            USING (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR EXISTS (
                    SELECT 1
                    FROM "PortfolioAlbums" album
                    WHERE album."Id" = "PortfolioImages"."PortfolioAlbumId"
                      AND album."OwnerUserId" = current_setting('app.current_user_id', true)
                )
            )
            WITH CHECK (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR EXISTS (
                    SELECT 1
                    FROM "PortfolioAlbums" album
                    WHERE album."Id" = "PortfolioImages"."PortfolioAlbumId"
                      AND album."OwnerUserId" = current_setting('app.current_user_id', true)
                )
            );

            CREATE POLICY "PortfolioImages_Delete"
            ON "PortfolioImages"
            FOR DELETE
            USING (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR EXISTS (
                    SELECT 1
                    FROM "PortfolioAlbums" album
                    WHERE album."Id" = "PortfolioImages"."PortfolioAlbumId"
                      AND album."OwnerUserId" = current_setting('app.current_user_id', true)
                )
            );

            ALTER TABLE "UserAlbumAccesses" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE "UserAlbumAccesses" FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS "UserAlbumAccesses_Select" ON "UserAlbumAccesses";
            DROP POLICY IF EXISTS "UserAlbumAccesses_AdminWrite" ON "UserAlbumAccesses";

            CREATE POLICY "UserAlbumAccesses_Select"
            ON "UserAlbumAccesses"
            FOR SELECT
            USING (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR "UserId" = current_setting('app.current_user_id', true)
            );

            CREATE POLICY "UserAlbumAccesses_AdminWrite"
            ON "UserAlbumAccesses"
            FOR ALL
            USING (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
            )
            WITH CHECK (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
            );

            ALTER TABLE "PrintRequests" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE "PrintRequests" FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS "PrintRequests_Select" ON "PrintRequests";
            DROP POLICY IF EXISTS "PrintRequests_Insert" ON "PrintRequests";
            DROP POLICY IF EXISTS "PrintRequests_AdminWrite" ON "PrintRequests";

            CREATE POLICY "PrintRequests_Select"
            ON "PrintRequests"
            FOR SELECT
            USING (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR "UserId" = current_setting('app.current_user_id', true)
            );

            CREATE POLICY "PrintRequests_Insert"
            ON "PrintRequests"
            FOR INSERT
            WITH CHECK (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR "UserId" = current_setting('app.current_user_id', true)
            );

            CREATE POLICY "PrintRequests_AdminWrite"
            ON "PrintRequests"
            FOR UPDATE
            USING (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
            )
            WITH CHECK (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
            );

            ALTER TABLE "PrintRequestItems" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE "PrintRequestItems" FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS "PrintRequestItems_Select" ON "PrintRequestItems";
            DROP POLICY IF EXISTS "PrintRequestItems_Insert" ON "PrintRequestItems";
            DROP POLICY IF EXISTS "PrintRequestItems_AdminWrite" ON "PrintRequestItems";

            CREATE POLICY "PrintRequestItems_Select"
            ON "PrintRequestItems"
            FOR SELECT
            USING (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR EXISTS (
                    SELECT 1
                    FROM "PrintRequests" request
                    WHERE request."Id" = "PrintRequestItems"."PrintRequestId"
                      AND request."UserId" = current_setting('app.current_user_id', true)
                )
            );

            CREATE POLICY "PrintRequestItems_Insert"
            ON "PrintRequestItems"
            FOR INSERT
            WITH CHECK (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
                OR EXISTS (
                    SELECT 1
                    FROM "PrintRequests" request
                    WHERE request."Id" = "PrintRequestItems"."PrintRequestId"
                      AND request."UserId" = current_setting('app.current_user_id', true)
                )
            );

            CREATE POLICY "PrintRequestItems_AdminWrite"
            ON "PrintRequestItems"
            FOR UPDATE
            USING (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
            )
            WITH CHECK (
                current_setting('app.current_is_system', true) = 'true'
                OR current_setting('app.current_is_admin', true) = 'true'
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "PrintRequestItems" DISABLE ROW LEVEL SECURITY;
            ALTER TABLE "PrintRequests" DISABLE ROW LEVEL SECURITY;
            ALTER TABLE "UserAlbumAccesses" DISABLE ROW LEVEL SECURITY;
            ALTER TABLE "PortfolioImages" DISABLE ROW LEVEL SECURITY;
            ALTER TABLE "PortfolioAlbums" DISABLE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS "PrintRequestItems_Select" ON "PrintRequestItems";
            DROP POLICY IF EXISTS "PrintRequestItems_Insert" ON "PrintRequestItems";
            DROP POLICY IF EXISTS "PrintRequestItems_AdminWrite" ON "PrintRequestItems";
            DROP POLICY IF EXISTS "PrintRequests_Select" ON "PrintRequests";
            DROP POLICY IF EXISTS "PrintRequests_Insert" ON "PrintRequests";
            DROP POLICY IF EXISTS "PrintRequests_AdminWrite" ON "PrintRequests";
            DROP POLICY IF EXISTS "UserAlbumAccesses_Select" ON "UserAlbumAccesses";
            DROP POLICY IF EXISTS "UserAlbumAccesses_AdminWrite" ON "UserAlbumAccesses";
            DROP POLICY IF EXISTS "PortfolioImages_Select" ON "PortfolioImages";
            DROP POLICY IF EXISTS "PortfolioImages_Insert" ON "PortfolioImages";
            DROP POLICY IF EXISTS "PortfolioImages_Update" ON "PortfolioImages";
            DROP POLICY IF EXISTS "PortfolioImages_Delete" ON "PortfolioImages";
            DROP POLICY IF EXISTS "PortfolioAlbums_Select" ON "PortfolioAlbums";
            DROP POLICY IF EXISTS "PortfolioAlbums_Insert" ON "PortfolioAlbums";
            DROP POLICY IF EXISTS "PortfolioAlbums_Update" ON "PortfolioAlbums";
            DROP POLICY IF EXISTS "PortfolioAlbums_Delete" ON "PortfolioAlbums";
            """);
    }
}
