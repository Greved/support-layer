using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class SeedDirectories : Migration
    {
        // Stable GUIDs so the migration is idempotent across environments.
        private static readonly Guid _planFree       = new("00000001-0000-0000-0000-000000000001");
        private static readonly Guid _planStarter    = new("00000001-0000-0000-0000-000000000002");
        private static readonly Guid _planPro        = new("00000001-0000-0000-0000-000000000003");
        private static readonly Guid _planEnterprise = new("00000001-0000-0000-0000-000000000004");

        private static readonly Guid _roleOwner  = new("00000002-0000-0000-0000-000000000001");
        private static readonly Guid _roleAdmin  = new("00000002-0000-0000-0000-000000000002");
        private static readonly Guid _roleMember = new("00000002-0000-0000-0000-000000000003");
        private static readonly Guid _roleViewer = new("00000002-0000-0000-0000-000000000004");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Plans
            migrationBuilder.InsertData(
                table: "Plans",
                columns: ["Id", "Name", "Slug", "IsActive"],
                values: new object[,]
                {
                    { _planFree,       "Free",       "free",       true },
                    { _planStarter,    "Starter",    "starter",    true },
                    { _planPro,        "Pro",        "pro",        true },
                    { _planEnterprise, "Enterprise", "enterprise", true }
                });

            // Roles
            migrationBuilder.InsertData(
                table: "Roles",
                columns: ["Id", "Name", "Slug", "IsActive"],
                values: new object[,]
                {
                    { _roleOwner,  "Owner",  "owner",  true },
                    { _roleAdmin,  "Admin",  "admin",  true },
                    { _roleMember, "Member", "member", true },
                    { _roleViewer, "Viewer", "viewer", true }
                });

            // PlanLimits (-1 = unlimited)
            migrationBuilder.InsertData(
                table: "PlanLimits",
                columns: ["Id", "PlanId", "MaxDocuments", "MaxStorageBytes", "MaxQueriesPerMonth", "MaxUsers"],
                values: new object[,]
                {
                    { Guid.NewGuid(), _planFree,       50,   524288000L,   500,   3  },
                    { Guid.NewGuid(), _planStarter,    500,  5368709120L,  5000,  10 },
                    { Guid.NewGuid(), _planPro,        5000, 53687091200L, 50000, 50 },
                    { Guid.NewGuid(), _planEnterprise, -1,   -1L,          -1,    -1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData("PlanLimits", "PlanId", new object[]
                { _planFree, _planStarter, _planPro, _planEnterprise });
            migrationBuilder.DeleteData("Roles", "Id", new object[]
                { _roleOwner, _roleAdmin, _roleMember, _roleViewer });
            migrationBuilder.DeleteData("Plans", "Id", new object[]
                { _planFree, _planStarter, _planPro, _planEnterprise });
        }
    }
}
