using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Querio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WidenIngestionJobsForCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ingestion_jobs_document_id",
                table: "ingestion_jobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "document_id",
                table: "ingestion_jobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // Backfilled as IngestDocument rather than EF's generated 0, which is not a value
            // the enum defines. Every job that existed before this column did was an
            // ingestion, so that is what they are.
            migrationBuilder.AddColumn<int>(
                name: "kind",
                table: "ingestion_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<string>(
                name: "storage_key",
                table: "ingestion_jobs",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_jobs_document_id",
                table: "ingestion_jobs",
                column: "document_id",
                unique: true,
                filter: "document_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ingestion_jobs_document_id",
                table: "ingestion_jobs");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "ingestion_jobs");

            migrationBuilder.DropColumn(
                name: "storage_key",
                table: "ingestion_jobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "document_id",
                table: "ingestion_jobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ingestion_jobs_document_id",
                table: "ingestion_jobs",
                column: "document_id",
                unique: true);
        }
    }
}
