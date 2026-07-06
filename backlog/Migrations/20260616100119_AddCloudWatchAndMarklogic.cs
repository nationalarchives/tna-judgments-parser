using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backlog.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudWatchAndMarklogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CloudwatchIngesterRunSummaries",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MarkLogicUri = table.Column<string>(type: "TEXT", nullable: true),
                    TreReference = table.Column<Guid>(type: "TEXT", nullable: true),
                    NcnReference = table.Column<string>(type: "TEXT", nullable: true),
                    LastInfoMessage = table.Column<string>(type: "TEXT", nullable: true),
                    LastWarningMessage = table.Column<string>(type: "TEXT", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
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
        }
    }
}
