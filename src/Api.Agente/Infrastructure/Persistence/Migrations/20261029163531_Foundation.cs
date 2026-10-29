using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Agente.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "agente");

            migrationBuilder.CreateTable(
                name: "executions",
                schema: "agente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatorId = table.Column<string>(type: "text", nullable: false),
                    ContextId = table.Column<string>(type: "text", nullable: false),
                    IdentityAssurance = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReportExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Stage = table.Column<string>(type: "text", nullable: false),
                    Verdict = table.Column<string>(type: "text", nullable: false),
                    LayoutVersion = table.Column<string>(type: "text", nullable: true),
                    DocumentRevisionSha256 = table.Column<string>(type: "text", nullable: true),
                    RuleCatalogVersion = table.Column<string>(type: "text", nullable: true),
                    ConfigurationSnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    BytesReceived = table.Column<long>(type: "bigint", nullable: false),
                    OriginalSha256 = table.Column<string>(type: "text", nullable: true),
                    RequiresHumanReview = table.Column<bool>(type: "boolean", nullable: false),
                    LimitationCodes = table.Column<string[]>(type: "text[]", nullable: false),
                    TechnicalFailureCode = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_executions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "provider_call_attempts",
                schema: "agente",
                columns: table => new
                {
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalCallId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IngestionJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceOwner = table.Column<string>(type: "text", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    RequestedModel = table.Column<string>(type: "text", nullable: false),
                    ReturnedModel = table.Column<string>(type: "text", nullable: true),
                    ProviderRequestId = table.Column<string>(type: "text", nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationMilliseconds = table.Column<double>(type: "double precision", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TokensInput = table.Column<long>(type: "bigint", nullable: true),
                    TokensOutput = table.Column<long>(type: "bigint", nullable: true),
                    CachedTokens = table.Column<long>(type: "bigint", nullable: true),
                    EmbeddingTokens = table.Column<long>(type: "bigint", nullable: true),
                    UsageState = table.Column<string>(type: "text", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    Currency = table.Column<string>(type: "text", nullable: true),
                    PriceBookVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EstimateStatus = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_call_attempts", x => x.AttemptId);
                });

            migrationBuilder.CreateTable(
                name: "evidence",
                schema: "agente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    RevisionSha256 = table.Column<string>(type: "text", nullable: true),
                    ChunkId = table.Column<string>(type: "text", nullable: true),
                    Pages = table.Column<int[]>(type: "integer[]", nullable: false),
                    Locator = table.Column<string>(type: "text", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evidence_executions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalSchema: "agente",
                        principalTable: "executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "findings",
                schema: "agente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Origin = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Location_Line = table.Column<long>(type: "bigint", nullable: true),
                    Location_ByteOffset = table.Column<long>(type: "bigint", nullable: true),
                    Location_RecordCode = table.Column<string>(type: "text", nullable: true),
                    Location_FieldPosition = table.Column<long>(type: "bigint", nullable: true),
                    Location_UnavailableReason = table.Column<string>(type: "text", nullable: true),
                    EvidenceIds = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_findings_executions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalSchema: "agente",
                        principalTable: "executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                schema: "agente",
                columns: table => new
                {
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractVersion = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    AnalyticContent = table.Column<string>(type: "jsonb", nullable: true),
                    RequiresHumanReview = table.Column<bool>(type: "boolean", nullable: false),
                    LimitationCodes = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.ExecutionId);
                    table.ForeignKey(
                        name: "FK_reports_executions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalSchema: "agente",
                        principalTable: "executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rule_evaluations",
                schema: "agente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<string>(type: "text", nullable: false),
                    OccurrenceKey = table.Column<string>(type: "text", nullable: false),
                    Applicability = table.Column<string>(type: "text", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_evaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rule_evaluations_executions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalSchema: "agente",
                        principalTable: "executions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_ExecutionId_Id",
                schema: "agente",
                table: "evidence",
                columns: new[] { "ExecutionId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_executions_CorrelationId",
                schema: "agente",
                table: "executions",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_findings_ExecutionId_Id",
                schema: "agente",
                table: "findings",
                columns: new[] { "ExecutionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_attempts_ExecutionId_StartedAt",
                schema: "agente",
                table: "provider_call_attempts",
                columns: new[] { "ExecutionId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_attempts_LogicalCallId_AttemptNumber",
                schema: "agente",
                table: "provider_call_attempts",
                columns: new[] { "LogicalCallId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reports_ExpiresAt",
                schema: "agente",
                table: "reports",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_rule_evaluations_ExecutionId_RuleId_OccurrenceKey",
                schema: "agente",
                table: "rule_evaluations",
                columns: new[] { "ExecutionId", "RuleId", "OccurrenceKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence",
                schema: "agente");

            migrationBuilder.DropTable(
                name: "findings",
                schema: "agente");

            migrationBuilder.DropTable(
                name: "provider_call_attempts",
                schema: "agente");

            migrationBuilder.DropTable(
                name: "reports",
                schema: "agente");

            migrationBuilder.DropTable(
                name: "rule_evaluations",
                schema: "agente");

            migrationBuilder.DropTable(
                name: "executions",
                schema: "agente");
        }
    }
}
