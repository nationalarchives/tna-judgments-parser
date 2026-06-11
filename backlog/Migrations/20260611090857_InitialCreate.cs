using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backlog.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParserEvents",
                columns: table => new
                {
                    SourceUuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParserRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Court = table.Column<string>(type: "TEXT", nullable: true),
                    FileExtension = table.Column<string>(type: "TEXT", nullable: true),
                    TrackerStatus = table.Column<string>(type: "TEXT", nullable: false),
                    TreReference = table.Column<string>(type: "TEXT", nullable: true),
                    Ncn = table.Column<string>(type: "TEXT", nullable: true),
                    CaseName = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalFileName = table.Column<string>(type: "TEXT", nullable: true),
                    DocumentContentHash = table.Column<string>(type: "TEXT", nullable: true),
                    CsvMetadataHash = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    TrackerLineLastUpdated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserEvents", x => new { x.SourceUuid, x.ParserRunId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserEvents");
        }
    }
}
