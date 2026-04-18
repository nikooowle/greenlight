using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class OperatorOverride_AndSubprocessRunHasOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasOverrides",
                table: "SubprocessRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "OperatorOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    McpRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubprocessId = table.Column<int>(type: "INTEGER", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    TicketRef = table.Column<string>(type: "TEXT", nullable: true),
                    EvidenceUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Operator = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatorOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperatorOverrides_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperatorOverrides_McpRuns_McpRunId",
                        column: x => x.McpRunId,
                        principalTable: "McpRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperatorOverrides_Subprocesses_SubprocessId",
                        column: x => x.SubprocessId,
                        principalTable: "Subprocesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorOverrides_LocationId",
                table: "OperatorOverrides",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperatorOverrides_McpRunId_LocationId_SubprocessId_StepName",
                table: "OperatorOverrides",
                columns: new[] { "McpRunId", "LocationId", "SubprocessId", "StepName" });

            migrationBuilder.CreateIndex(
                name: "IX_OperatorOverrides_SubprocessId",
                table: "OperatorOverrides",
                column: "SubprocessId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperatorOverrides");

            migrationBuilder.DropColumn(
                name: "HasOverrides",
                table: "SubprocessRuns");
        }
    }
}
