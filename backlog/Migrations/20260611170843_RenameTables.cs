using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backlog.Migrations
{
    /// <inheritdoc />
    public partial class RenameTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CloudwatchSummaryLogLines");

            migrationBuilder.DropTable(
                name: "MarkLogicParserRunDocuments");

            migrationBuilder.CreateTable(
                name: "CloudwatchIngesterRunSummaries",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarkLogicUri = table.Column<string>(type: "TEXT", nullable: false),
                    TreReference = table.Column<Guid>(type: "TEXT", nullable: false),
                    NcnReference = table.Column<string>(type: "TEXT", nullable: false),
                    LastInfoMessage = table.Column<string>(type: "TEXT", nullable: false),
                    LastWarningMessage = table.Column<string>(type: "TEXT", nullable: false),
                    LastErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    LambdaReport = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloudwatchIngesterRunSummaries", x => x.RequestId);
                });

            migrationBuilder.CreateTable(
                name: "MarkLogicDocumentStatuses",
                columns: table => new
                {
                    FakeTreUuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentUri = table.Column<string>(type: "TEXT", nullable: false),
                    Published = table.Column<bool>(type: "INTEGER", nullable: false),
                    AwsRequestId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkLogicDocumentStatuses", x => x.FakeTreUuid);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CloudwatchIngesterRunSummaries_NcnReference",
                table: "CloudwatchIngesterRunSummaries",
                column: "NcnReference");

            migrationBuilder.CreateIndex(
                name: "IX_CloudwatchIngesterRunSummaries_TreReference",
                table: "CloudwatchIngesterRunSummaries",
                column: "TreReference");

            migrationBuilder.CreateIndex(
                name: "IX_MarkLogicDocumentStatuses_AwsRequestId",
                table: "MarkLogicDocumentStatuses",
                column: "AwsRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CloudwatchIngesterRunSummaries");

            migrationBuilder.DropTable(
                name: "MarkLogicDocumentStatuses");

            migrationBuilder.CreateTable(
                name: "CloudwatchSummaryLogLines",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LambdaReport = table.Column<string>(type: "TEXT", nullable: false),
                    LastErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    LastInfoMessage = table.Column<string>(type: "TEXT", nullable: false),
                    LastWarningMessage = table.Column<string>(type: "TEXT", nullable: false),
                    MarkLogicUri = table.Column<string>(type: "TEXT", nullable: false),
                    NcnReference = table.Column<string>(type: "TEXT", nullable: false),
                    TreReference = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloudwatchSummaryLogLines", x => x.RequestId);
                });

            migrationBuilder.CreateTable(
                name: "MarkLogicParserRunDocuments",
                columns: table => new
                {
                    FakeTreUuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    AwsRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentUri = table.Column<string>(type: "TEXT", nullable: false),
                    Published = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkLogicParserRunDocuments", x => x.FakeTreUuid);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CloudwatchSummaryLogLines_NcnReference",
                table: "CloudwatchSummaryLogLines",
                column: "NcnReference");

            migrationBuilder.CreateIndex(
                name: "IX_CloudwatchSummaryLogLines_TreReference",
                table: "CloudwatchSummaryLogLines",
                column: "TreReference");

            migrationBuilder.CreateIndex(
                name: "IX_MarkLogicParserRunDocuments_AwsRequestId",
                table: "MarkLogicParserRunDocuments",
                column: "AwsRequestId");
        }
    }
}
