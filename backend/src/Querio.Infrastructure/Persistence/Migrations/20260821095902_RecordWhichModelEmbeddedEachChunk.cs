using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Querio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordWhichModelEmbeddedEachChunk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "embedding_model",
                table: "document_chunks",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // Every vector that exists at this point came from one provider, so naming it is a
            // statement of fact rather than a guess. Left nullable rather than made required:
            // the column has to be added before anything can write it, and a default would
            // quietly relabel a future provider's vectors as this one's.
            migrationBuilder.Sql(
                """
                UPDATE document_chunks
                SET embedding_model = 'gemini-embedding-001@768'
                WHERE embedding IS NOT NULL AND embedding_model IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "embedding_model",
                table: "document_chunks");
        }
    }
}
