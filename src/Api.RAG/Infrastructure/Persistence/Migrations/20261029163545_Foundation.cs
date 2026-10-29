using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.RAG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rag");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "provider_call_attempts",
                schema: "rag",
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

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_attempts_ExecutionId_StartedAt",
                schema: "rag",
                table: "provider_call_attempts",
                columns: new[] { "ExecutionId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_attempts_IngestionJobId_StartedAt",
                schema: "rag",
                table: "provider_call_attempts",
                columns: new[] { "IngestionJobId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_call_attempts_LogicalCallId_AttemptNumber",
                schema: "rag",
                table: "provider_call_attempts",
                columns: new[] { "LogicalCallId", "AttemptNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_call_attempts",
                schema: "rag");
        }
    }
}
