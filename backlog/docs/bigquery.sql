
WITH LatestParserEvents AS (
                    SELECT
                        *,
                        ROW_NUMBER() OVER (PARTITION BY SourceUuid ORDER BY TrackerLineLastUpdated DESC) AS rn
                    FROM ParserEvents
                )
SELECT
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
END AS DocumentStatus,
CASE
    WHEN mlds.DocumentUri is null THEN null
    ELSE TRIM(CONCAT("https://editor.caselaw.nationalarchives.gov.uk", mlds.DocumentUri), ".xml" )
END as EuiLink,
  substr(
    cirs.LastErrorMessage,
    length(cirs.LastErrorMessage) - instr(reverse(cirs.LastErrorMessage), char(10)) + 2
  ) as Error,
*

FROM LatestParserEvents lpe
LEFT JOIN MarkLogicDocumentStatuses mlds ON lpe.TreReference = mlds.FakeTreUuid
LEFT JOIN CloudwatchIngesterRunSummaries cirs ON lpe.TreReference = cirs.TreReference
