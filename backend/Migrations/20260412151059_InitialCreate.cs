using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Region = table.Column<string>(type: "TEXT", nullable: true),
                    InScope = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReportMonth = table.Column<string>(type: "TEXT", nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EomDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subprocesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Phase = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subprocesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    McpRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Process = table.Column<string>(type: "TEXT", nullable: false),
                    ScriptName = table.Column<string>(type: "TEXT", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", nullable: false),
                    StateName = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StartMarker = table.Column<string>(type: "TEXT", nullable: true),
                    EndMarker = table.Column<string>(type: "TEXT", nullable: true),
                    Iteration = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRuntimeHours = table.Column<double>(type: "REAL", nullable: false),
                    FailedRuntimeHours = table.Column<double>(type: "REAL", nullable: false),
                    EfficientRuntimeHours = table.Column<double>(type: "REAL", nullable: false),
                    OpportunityCostHours = table.Column<double>(type: "REAL", nullable: false),
                    InefficientRuntimeHours = table.Column<double>(type: "REAL", nullable: false),
                    E2ERuntimeHours = table.Column<double>(type: "REAL", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    NextStarted = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessLogEntries_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessLogEntries_McpRuns_McpRunId",
                        column: x => x.McpRunId,
                        principalTable: "McpRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LocationStepRegistry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubprocessId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiredStepName = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsMandatory = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationStepRegistry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationStepRegistry_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocationStepRegistry_Subprocesses_SubprocessId",
                        column: x => x.SubprocessId,
                        principalTable: "Subprocesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScriptMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RawScriptPattern = table.Column<string>(type: "TEXT", nullable: false),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubprocessId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScriptMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScriptMappings_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScriptMappings_Subprocesses_SubprocessId",
                        column: x => x.SubprocessId,
                        principalTable: "Subprocesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlaTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubprocessId = table.Column<int>(type: "INTEGER", nullable: false),
                    Frequency = table.Column<string>(type: "TEXT", nullable: false),
                    Deadline = table.Column<string>(type: "TEXT", nullable: false),
                    Workday = table.Column<int>(type: "INTEGER", nullable: false),
                    SlaDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlaTargets_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SlaTargets_Subprocesses_SubprocessId",
                        column: x => x.SubprocessId,
                        principalTable: "Subprocesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubprocessRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    McpRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    LocationId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubprocessId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ElapsedMinutes = table.Column<double>(type: "REAL", nullable: true),
                    CompletedSteps = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRequiredSteps = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubprocessRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubprocessRuns_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubprocessRuns_McpRuns_McpRunId",
                        column: x => x.McpRunId,
                        principalTable: "McpRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubprocessRuns_Subprocesses_SubprocessId",
                        column: x => x.SubprocessId,
                        principalTable: "Subprocesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Issues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SubprocessRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    RootCauseCategory = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IncidentNumber = table.Column<string>(type: "TEXT", nullable: true),
                    OperatorComment = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Issues_SubprocessRuns_SubprocessRunId",
                        column: x => x.SubprocessRunId,
                        principalTable: "SubprocessRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_SubprocessRunId",
                table: "Issues",
                column: "SubprocessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Code",
                table: "Locations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationStepRegistry_LocationId_SubprocessId_RequiredStepName",
                table: "LocationStepRegistry",
                columns: new[] { "LocationId", "SubprocessId", "RequiredStepName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationStepRegistry_SubprocessId",
                table: "LocationStepRegistry",
                column: "SubprocessId");

            migrationBuilder.CreateIndex(
                name: "IX_McpRuns_ReportMonth",
                table: "McpRuns",
                column: "ReportMonth",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessLogEntries_LocationId",
                table: "ProcessLogEntries",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessLogEntries_McpRunId_LocationId_ScriptName_StepName_Iteration",
                table: "ProcessLogEntries",
                columns: new[] { "McpRunId", "LocationId", "ScriptName", "StepName", "Iteration" });

            migrationBuilder.CreateIndex(
                name: "IX_ScriptMappings_LocationId_RawScriptPattern",
                table: "ScriptMappings",
                columns: new[] { "LocationId", "RawScriptPattern" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScriptMappings_SubprocessId",
                table: "ScriptMappings",
                column: "SubprocessId");

            migrationBuilder.CreateIndex(
                name: "IX_SlaTargets_LocationId_SubprocessId",
                table: "SlaTargets",
                columns: new[] { "LocationId", "SubprocessId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlaTargets_SubprocessId",
                table: "SlaTargets",
                column: "SubprocessId");

            migrationBuilder.CreateIndex(
                name: "IX_Subprocesses_Name",
                table: "Subprocesses",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubprocessRuns_LocationId",
                table: "SubprocessRuns",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_SubprocessRuns_McpRunId_LocationId_SubprocessId",
                table: "SubprocessRuns",
                columns: new[] { "McpRunId", "LocationId", "SubprocessId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubprocessRuns_SubprocessId",
                table: "SubprocessRuns",
                column: "SubprocessId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Issues");

            migrationBuilder.DropTable(
                name: "LocationStepRegistry");

            migrationBuilder.DropTable(
                name: "ProcessLogEntries");

            migrationBuilder.DropTable(
                name: "ScriptMappings");

            migrationBuilder.DropTable(
                name: "SlaTargets");

            migrationBuilder.DropTable(
                name: "SubprocessRuns");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "McpRuns");

            migrationBuilder.DropTable(
                name: "Subprocesses");
        }
    }
}
