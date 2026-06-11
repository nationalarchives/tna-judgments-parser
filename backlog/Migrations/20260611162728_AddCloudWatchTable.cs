using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backlog.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudWatchTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CloudwatchSummaryLogLines",
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
                    table.PrimaryKey("PK_CloudwatchSummaryLogLines", x => x.RequestId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CloudwatchSummaryLogLines_NcnReference",
                table: "CloudwatchSummaryLogLines",
                column: "NcnReference");

            migrationBuilder.CreateIndex(
                name: "IX_CloudwatchSummaryLogLines_TreReference",
                table: "CloudwatchSummaryLogLines",
                column: "TreReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CloudwatchSummaryLogLines");
        }
    }
}
