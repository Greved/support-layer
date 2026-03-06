using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class Phase6EvalDatasets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvalDatasets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceFeedbackId = table.Column<Guid>(type: "uuid", nullable: true),
                    Question = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    GroundTruth = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    SourceChunkIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    QuestionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DatasetVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalDatasets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvalDatasets_ChatMessageFeedback_SourceFeedbackId",
                        column: x => x.SourceFeedbackId,
                        principalTable: "ChatMessageFeedback",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EvalDatasets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvalDatasets_SourceFeedbackId",
                table: "EvalDatasets",
                column: "SourceFeedbackId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvalDatasets_TenantId_CreatedAt",
                table: "EvalDatasets",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvalDatasets");
        }
    }
}
