#!/usr/bin/env bash
set -euo pipefail

usage() {
    echo "Usage: $0 [--db <file>] [--cloudwatch <file>] [--marklogic <file>]"
    echo ""
    echo "  Any omitted file argument will open a file picker dialog."
    echo ""
    echo "  --db               SQLite parser tracker database"
    echo "  --cloudwatch       CloudWatch CSV to insert into CloudwatchIngesterRunSummaries"
    echo "  --marklogic        MarkLogic CSV to insert into MarkLogicDocumentStatuses"
    exit "${1:-1}"
}

pick_file() {
    osascript -e "POSIX path of (choose file with prompt \"$1\")" \
        || { echo "Selection cancelled."; exit 1; }
}

command -v sqlite3 >/dev/null 2>&1 || { echo "sqlite3 is not installed."; exit 1; }

PARSER_DB=""
CLOUDWATCH_CSV=""
MARKLOGIC_CSV=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --database|--db) PARSER_DB="$2";      shift 2 ;;
        --cloudwatch)    CLOUDWATCH_CSV="$2"; shift 2 ;;
        --marklogic)     MARKLOGIC_CSV="$2";  shift 2 ;;
        --help|-h) usage 0 ;;
        *) echo "Unknown argument: $1"; usage ;;
    esac
done

[[ -z "$PARSER_DB"      ]] && PARSER_DB=$(pick_file  "Select Parser Tracker SQLite DB")
[[ -z "$CLOUDWATCH_CSV" ]] && CLOUDWATCH_CSV=$(pick_file "Select CloudWatch CSV")
[[ -z "$MARKLOGIC_CSV"  ]] && MARKLOGIC_CSV=$(pick_file  "Select MarkLogic CSV")

for f in "$PARSER_DB" "$CLOUDWATCH_CSV" "$MARKLOGIC_CSV"; do
    [[ -f "$f" ]] || { echo "File not found: $f"; exit 1; }
done

echo ""
echo "Importing CloudWatch and MarkLogic CSVs into:"
echo "  ${PARSER_DB}"

sqlite3 "$PARSER_DB" <<SQL
PRAGMA foreign_keys = OFF;

DROP TABLE IF EXISTS temp_cloudwatch_import;
DROP TABLE IF EXISTS temp_marklogic_import;

.mode csv
.import '${CLOUDWATCH_CSV}' temp_cloudwatch_import
.import '${MARKLOGIC_CSV}' temp_marklogic_import

.mode box

--Cloudwatch csv header:     @requestId,markLogicUri,treReference,ncnReference,lastInfoMessage,lastWarningMessage,lastErrorMessage,lambdaReport
INSERT INTO CloudwatchIngesterRunSummaries (
    RequestId,
    LambdaReport,
    LastErrorMessage,
    LastInfoMessage,
    LastWarningMessage,
    MarkLogicUri,
    NcnReference,
    TreReference
)
SELECT
    "@requestId",
    COALESCE(lambdaReport, ''),
    NULLIF(lastErrorMessage, ''),
    NULLIF(lastInfoMessage, ''),
    NULLIF(lastWarningMessage, ''),
    NULLIF(markLogicUri, ''),
    NULLIF(ncnReference, ''),
    NULLIF(treReference, '')
FROM temp_cloudwatch_import
WHERE NULLIF("@requestId", '') IS NOT NULL;


--Marklogic csv header:    document_URI,fake_TRE_UUID,published,AWS_request_id
INSERT INTO MarkLogicDocumentStatuses (
    FakeTreUuid,
    DocumentUri,
    Published,
    AwsRequestId
)
SELECT
    fake_TRE_UUID,
    COALESCE(document_URI, ''),
    CASE LOWER(COALESCE(published, ''))
        WHEN 'true' THEN 1
        WHEN '1' THEN 1
        WHEN 'yes' THEN 1
        ELSE 0
    END,
    COALESCE(AWS_request_id, '')
FROM temp_marklogic_import
WHERE NULLIF(fake_TRE_UUID, '') IS NOT NULL;
SQL

echo ""
echo "Tracker database updated successfully"
echo ""
echo "To explore the tracker database interactively:"
echo "  sqlite3 \"${PARSER_DB}\""
echo ""
