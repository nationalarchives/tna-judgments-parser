using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backlog.Migrations
{
    /// <inheritdoc />
    public partial class AddMarkLogicParserRunDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarkLogicParserRunDocuments",
                columns: table => new
                {
                    FakeTreUuid = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentUri = table.Column<string>(type: "TEXT", nullable: false),
                    Published = table.Column<bool>(type: "INTEGER", nullable: false),
                    AwsRequestId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkLogicParserRunDocuments", x => x.FakeTreUuid);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarkLogicParserRunDocuments_AwsRequestId",
                table: "MarkLogicParserRunDocuments",
                column: "AwsRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarkLogicParserRunDocuments");
        }
    }
}
