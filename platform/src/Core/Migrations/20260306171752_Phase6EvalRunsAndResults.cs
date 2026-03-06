using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class Phase6EvalRunsAndResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvalRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConfigSnapshotJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    TriggeredBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvalRuns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvalResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Answer = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    RetrievedChunksJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Faithfulness = table.Column<double>(type: "double precision", nullable: true),
                    AnswerRelevancy = table.Column<double>(type: "double precision", nullable: true),
                    ContextPrecision = table.Column<double>(type: "double precision", nullable: true),
                    ContextRecall = table.Column<double>(type: "double precision", nullable: true),
                    HallucinationScore = table.Column<double>(type: "double precision", nullable: true),
                    AnswerCompleteness = table.Column<double>(type: "double precision", nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvalResults_EvalDatasets_DatasetItemId",
                        column: x => x.DatasetItemId,
                        principalTable: "EvalDatasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EvalResults_EvalRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "EvalRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvalResults_DatasetItemId",
                table: "EvalResults",
                column: "DatasetItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalResults_RunId",
                table: "EvalResults",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_EvalRuns_TenantId_StartedAt",
                table: "EvalRuns",
                columns: new[] { "TenantId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvalResults");

            migrationBuilder.DropTable(
                name: "EvalRuns");
        }
    }
}
