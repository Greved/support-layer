using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxRequestsPerMinute",
                table: "PlanLimits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ApiKeyId",
                table: "ChatSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_ApiKeyId",
                table: "ChatSessions",
                column: "ApiKeyId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSessions_ApiKeys_ApiKeyId",
                table: "ChatSessions",
                column: "ApiKeyId",
                principalTable: "ApiKeys",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatSessions_ApiKeys_ApiKeyId",
                table: "ChatSessions");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_ApiKeyId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "MaxRequestsPerMinute",
                table: "PlanLimits");

            migrationBuilder.DropColumn(
                name: "ApiKeyId",
                table: "ChatSessions");
        }
    }
}
