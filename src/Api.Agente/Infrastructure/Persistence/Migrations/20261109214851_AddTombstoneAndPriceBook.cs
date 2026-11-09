using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Agente.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTombstoneAndPriceBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "execution_tombstones",
                schema: "agente",
                columns: table => new
                {
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PurgeAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_tombstones", x => x.ExecutionId);
                });

            migrationBuilder.CreateTable(
                name: "price_books",
                schema: "agente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    QueriedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_books", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "price_book_rates",
                schema: "agente",
                columns: table => new
                {
                    PriceBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    InputPricePerMillion = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    OutputPricePerMillion = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    CachedPricePerMillion = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    EmbeddingPricePerMillion = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_book_rates", x => new { x.PriceBookId, x.Id });
                    table.ForeignKey(
                        name: "FK_price_book_rates_price_books_PriceBookId",
                        column: x => x.PriceBookId,
                        principalSchema: "agente",
                        principalTable: "price_books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_execution_tombstones_PurgeAt",
                schema: "agente",
                table: "execution_tombstones",
                column: "PurgeAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "execution_tombstones",
                schema: "agente");

            migrationBuilder.DropTable(
                name: "price_book_rates",
                schema: "agente");

            migrationBuilder.DropTable(
                name: "price_books",
                schema: "agente");
        }
    }
}
