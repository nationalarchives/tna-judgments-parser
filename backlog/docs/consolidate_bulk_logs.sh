#!/usr/bin/env bash
set -euo pipefail

usage() {
    echo "Usage: $0 [--parser <file>] [--cloudwatch <file>] [--marklogic <file>] [--output <file>]"
    echo "  Any omitted file argument will open a file picker dialog."
    exit "${1:-1}"
}

pick_file() {
    osascript -e "POSIX path of (choose file with prompt \"$1\")" \
        || { echo "Selection cancelled."; exit 1; }
}

pick_save() {
    osascript -e "POSIX path of (choose file name with prompt \"$1\" default name \"$2\")" \
        || { echo "Selection cancelled."; exit 1; }
}

show_if_any() {
    local status="$1" query="$2"
    local count
    count=$(duckdb -csv -c "SELECT COUNT(*) FROM '${OUTPUT_CSV}' WHERE status = '${status}';" | tail -1)
    [[ "$count" -gt 0 ]] || return 0
    echo ""
    echo "────────────────────────────── ${status} (${count}) ──────────────────────────────"
    duckdb -c "${query}"
}

command -v duckdb >/dev/null 2>&1 || { echo "duckdb is not installed."; exit 1; }

PARSER_CSV="" CLOUDWATCH_CSV="" MARKLOGIC_CSV="" OUTPUT_CSV=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --parser)     PARSER_CSV="$2";     shift 2 ;;
        --cloudwatch) CLOUDWATCH_CSV="$2"; shift 2 ;;
        --marklogic)  MARKLOGIC_CSV="$2";  shift 2 ;;
        --output)     OUTPUT_CSV="$2";     shift 2 ;;
        --help|-h) usage 0 ;;
        *) echo "Unknown argument: $1"; usage ;;
    esac
done

[[ -z "$PARSER_CSV"     ]] && PARSER_CSV=$(pick_file     "Select Parser Tracker CSV")
[[ -z "$CLOUDWATCH_CSV" ]] && CLOUDWATCH_CSV=$(pick_file "Select CloudWatch CSV")
[[ -z "$MARKLOGIC_CSV"  ]] && MARKLOGIC_CSV=$(pick_file  "Select MarkLogic CSV")
[[ -z "$OUTPUT_CSV"     ]] && OUTPUT_CSV=$(pick_save     "Save consolidated output as…" "output.csv")

for f in "$PARSER_CSV" "$CLOUDWATCH_CSV" "$MARKLOGIC_CSV"; do
    [[ -f "$f" ]] || { echo "File not found: $f"; exit 1; }
done

duckdb -c "
COPY (
    SELECT pt.* EXCLUDE rn, cw.* EXCLUDE treReference, ml.*,
        CASE
            WHEN ml.AWS_request_id IS NOT NULL AND ml.published = 'true'  THEN 'Published'
            WHEN ml.AWS_request_id IS NOT NULL AND ml.published = 'false' THEN 'Ingested'
            WHEN ml.AWS_request_id IS NOT NULL                            THEN 'unknown'
            WHEN cw.treReference   IS NOT NULL                            THEN 'FailedIngestion'
            ELSE pt.TrackerStatus
        END AS status
    FROM (
        SELECT *, ROW_NUMBER() OVER (
            PARTITION BY SourceUuid ORDER BY TrackerLineLastUpdated DESC
        ) AS rn
        FROM '${PARSER_CSV}'
    ) pt
    LEFT JOIN '${CLOUDWATCH_CSV}' cw
           ON pt.TreReference = cw.treReference
          AND pt.TreReference != ''
    LEFT JOIN '${MARKLOGIC_CSV}' ml
           ON cw.\"@requestId\" = ml.AWS_request_id
    WHERE rn = 1
) TO '${OUTPUT_CSV}' (HEADER, DELIMITER ',');
"

echo ""
duckdb -c "
SELECT status, COUNT(*) AS count FROM '${OUTPUT_CSV}' GROUP BY status ORDER BY count DESC;
SELECT COUNT(*) AS total FROM '${OUTPUT_CSV}';
"

show_if_any "Ingested" \
    "SELECT status, SourceUuid, TreReference, Ncn, document_URI, AWS_request_id, lambdaReport, published
     FROM '${OUTPUT_CSV}' WHERE status = 'Ingested';"

show_if_any "FailedIngestion" \
    "SELECT status, SourceUuid, TreReference, Ncn, document_URI, AWS_request_id, lastErrorMessage, lastWarningMessage
     FROM '${OUTPUT_CSV}' WHERE status = 'FailedIngestion';"

show_if_any "unknown" \
    "SELECT * FROM '${OUTPUT_CSV}' WHERE status = 'unknown';"

echo ""
echo "Output written to: ${OUTPUT_CSV}"
echo ""
echo "To explore the output interactively:"
echo "  duckdb -cmd \"CREATE VIEW output AS SELECT * FROM '${OUTPUT_CSV}';\""
echo ""