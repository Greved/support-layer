using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class Phase2SeedRateLimits : Migration
    {
        // Stable plan GUIDs from SeedDirectories migration
        private static readonly Guid _planFree       = new("00000001-0000-0000-0000-000000000001");
        private static readonly Guid _planStarter    = new("00000001-0000-0000-0000-000000000002");
        private static readonly Guid _planPro        = new("00000001-0000-0000-0000-000000000003");
        private static readonly Guid _planEnterprise = new("00000001-0000-0000-0000-000000000004");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE "PlanLimits" SET "MaxRequestsPerMinute" = CASE "PlanId"
                    WHEN '{_planFree}'       THEN 10
                    WHEN '{_planStarter}'    THEN 30
                    WHEN '{_planPro}'        THEN 100
                    WHEN '{_planEnterprise}' THEN 500
                    ELSE 10
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "PlanLimits" SET "MaxRequestsPerMinute" = 0
                """);
        }
    }
}
