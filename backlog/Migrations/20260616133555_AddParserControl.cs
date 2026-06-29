using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backlog.Migrations
{
    /// <inheritdoc />
    public partial class AddParserControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE VIEW ParserControlView AS
                WITH LatestParserEvents AS (
                    SELECT
                        SourceUuid,
                        TreReference,
                        TrackerStatus,
                        ROW_NUMBER() OVER (PARTITION BY SourceUuid ORDER BY TrackerLineLastUpdated DESC) AS rn
                    FROM ParserEvents
                )
                SELECT
                    lpe.SourceUuid,
                    CASE
                        WHEN EXISTS (
                            SELECT 1
                            FROM CloudwatchIngesterRunSummaries cirs
                            JOIN MarkLogicDocumentStatuses mlds ON cirs.RequestId = mlds.AwsRequestId
                            WHERE cirs.TreReference = lpe.TreReference AND mlds.Published = 1
                        ) THEN 'Published'
                        WHEN EXISTS (
                            SELECT 1
                            FROM CloudwatchIngesterRunSummaries cirs
                            JOIN MarkLogicDocumentStatuses mlds ON cirs.RequestId = mlds.AwsRequestId
                            WHERE cirs.TreReference = lpe.TreReference AND mlds.Published = 0
                        ) THEN 'PublicationFailed'
                        WHEN EXISTS (
                            SELECT 1
                            FROM CloudwatchIngesterRunSummaries cirs
                            WHERE cirs.TreReference = lpe.TreReference AND cirs.LastErrorMessage IS NOT NULL
                        ) THEN 'IngesterFailed'
                        WHEN EXISTS (
                            SELECT 1
                            FROM CloudwatchIngesterRunSummaries cirs
                            WHERE cirs.TreReference = lpe.TreReference
                        ) THEN 'Ingested'
                        ELSE lpe.TrackerStatus
                    END AS TrackerStatus
                FROM LatestParserEvents lpe
                WHERE lpe.rn = 1
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS ParserControlView");
        }
    }
}
