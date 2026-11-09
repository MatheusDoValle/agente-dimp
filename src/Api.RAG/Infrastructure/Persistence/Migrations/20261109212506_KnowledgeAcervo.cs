using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Api.RAG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeAcervo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documents",
                schema: "rag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Identification = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "embedding_profiles",
                schema: "rag",
                columns: table => new
                {
                    ProfileId = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Dimension = table.Column<int>(type: "integer", nullable: false),
                    Metric = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Normalized = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_embedding_profiles", x => x.ProfileId);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_chunk_checkpoints",
                schema: "rag",
                columns: table => new
                {
                    IngestionJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkHash = table.Column<string>(type: "text", nullable: false),
                    EmbeddingProfileId = table.Column<string>(type: "text", nullable: false),
                    CallAttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_chunk_checkpoints", x => new { x.IngestionJobId, x.ChunkHash, x.EmbeddingProfileId });
                });

            migrationBuilder.CreateTable(
                name: "ingestion_jobs",
                schema: "rag",
                columns: table => new
                {
                    IngestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExtractionProfileId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChunkProfileId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmbeddingProfileId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LimitationCodes = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_jobs", x => x.IngestionId);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_work_items",
                schema: "rag",
                columns: table => new
                {
                    IngestionJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FencingToken = table.Column<long>(type: "bigint", nullable: false),
                    CheckpointVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_work_items", x => x.IngestionJobId);
                });

            migrationBuilder.CreateTable(
                name: "retrieval_evaluations",
                schema: "rag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmbeddingProfileId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReferenceSetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecallAtK = table.Column<double>(type: "double precision", nullable: false),
                    Mrr = table.Column<double>(type: "double precision", nullable: false),
                    PassDecision = table.Column<bool>(type: "boolean", nullable: false),
                    MeasuredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retrieval_evaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_revisions",
                schema: "rag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    RevisionSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LayoutVersion = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RetrievedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    PageCount = table.Column<long>(type: "bigint", nullable: false),
                    SourceStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StoragePath = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_revisions_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "rag",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                schema: "rag",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Pages = table.Column<int[]>(type: "integer[]", nullable: false),
                    Section = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RecordCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    FieldCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ChunkHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_chunks_document_revisions_RevisionId",
                        column: x => x.RevisionId,
                        principalSchema: "rag",
                        principalTable: "document_revisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chunk_vectors",
                schema: "rag",
                columns: table => new
                {
                    ChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmbeddingProfileId = table.Column<string>(type: "text", nullable: false),
                    Vector = table.Column<Vector>(type: "vector(1536)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunk_vectors", x => new { x.ChunkId, x.EmbeddingProfileId });
                    table.ForeignKey(
                        name: "FK_chunk_vectors_embedding_profiles_EmbeddingProfileId",
                        column: x => x.EmbeddingProfileId,
                        principalSchema: "rag",
                        principalTable: "embedding_profiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_chunk_vectors_knowledge_chunks_ChunkId",
                        column: x => x.ChunkId,
                        principalSchema: "rag",
                        principalTable: "knowledge_chunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chunk_vectors_EmbeddingProfileId",
                schema: "rag",
                table: "chunk_vectors",
                column: "EmbeddingProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_document_revisions_DocumentId",
                schema: "rag",
                table: "document_revisions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_document_revisions_RevisionSha256",
                schema: "rag",
                table: "document_revisions",
                column: "RevisionSha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_jobs_RevisionSha256_ExtractionProfileId_ChunkProf~",
                schema: "rag",
                table: "ingestion_jobs",
                columns: new[] { "RevisionSha256", "ExtractionProfileId", "ChunkProfileId", "EmbeddingProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_work_items_AvailableAt_LeaseExpiresAt",
                schema: "rag",
                table: "ingestion_work_items",
                columns: new[] { "AvailableAt", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_RevisionId_ChunkHash",
                schema: "rag",
                table: "knowledge_chunks",
                columns: new[] { "RevisionId", "ChunkHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_RevisionId_RecordCode",
                schema: "rag",
                table: "knowledge_chunks",
                columns: new[] { "RevisionId", "RecordCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chunk_vectors",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "ingestion_chunk_checkpoints",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "ingestion_jobs",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "ingestion_work_items",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "retrieval_evaluations",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "embedding_profiles",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "knowledge_chunks",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "document_revisions",
                schema: "rag");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "rag");
        }
    }
}
