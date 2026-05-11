# Parser Run Reconciliation Playbook

This playbook is for backlog batch runs only.

## Files Used

- [docs/parser-run-reconciliation.xq](docs/parser-run-reconciliation.xq)
- [docs/parser-run-reconciliation-lib.xqy](docs/parser-run-reconciliation-lib.xqy)
- [docs/run-parser-run-reconciliation-tests.sh](docs/run-parser-run-reconciliation-tests.sh)
- [docs/run-parser-run-reconciliation-fixture-tests.sh](docs/run-parser-run-reconciliation-fixture-tests.sh)

## Inputs

- `parser-run-id-<parserRunId>.txt` from the backlog output folder
- `bundle-references-<parserRunId>.txt` from the backlog output folder
- `batch-manifest-<parserRunId>.csv` from the backlog output folder (optional trace file)
- MarkLogic access (Query Console or equivalent)
- CloudWatch Logs Insights access

## Identifier Rules

- `parser_run_id`: one value for the whole batch
- `bundle_reference`: unique TRE reference per bundle; this is the reconciliation key

## Step 1: Take The Reconciliation Inputs From Backlog Output

Backlog now writes the reconciliation inputs directly. Use the files from the same output folder as the batch bundles.

Required files:

- `parser-run-id-<parserRunId>.txt`
- `bundle-references-<parserRunId>.txt`

Sanity checks:

- `parser-run-id-<parserRunId>.txt` contains exactly one line
- `bundle-references-<parserRunId>.txt` is non-empty
- `bundle-references-<parserRunId>.txt` count should match processed bundle count

## Step 2: Run Reconciliation Query In MarkLogic

Run [docs/parser-run-reconciliation.xq](docs/parser-run-reconciliation.xq) with external variables:

- `$parserRunId`: value from `parser-run-id-<parserRunId>.txt`
- `$expectedBundleReferences`: all lines from `bundle-references-<parserRunId>.txt`

Query output includes:

- `counts.expected`
- `counts.ingested`
- `counts.published`
- `counts.missing`
- `counts.unpublished`
- arrays: `missing`, `unpublished`, `published`

Definitions:

- `missing = expected - ingested`
- `unpublished = ingested - published`

## Step 3: CloudWatch Accounting

Replace `<PARSER_RUN_ID>` and run:

```sql
fields @message
| filter @message like /<PARSER_RUN_ID>/
| stats
  countif(@message like /Received event/) as received,
  countif(@message like /Ingestion complete/) as ingestion_complete,
  countif(@message like /Runtime\.OutOfMemory/) as oom,
  countif(@message like /Task timed out|timed out/) as timeout,
  countif(@message like /bad doc|validation|unsupported|invalid/) as bad_doc
```

Compute:

- `unaccounted = received - (ingestion_complete + oom + timeout + bad_doc)`

If `unaccounted != 0`, run the next query.

## Step 4: Find Records With No Terminal Outcome

```sql
fields @timestamp, @message
| filter @message like /<PARSER_RUN_ID>/
| parse @message /documentId[=: ]+\"?(?<documentId>[^\",\s]+)/
| parse @message /(?<kind>Received event|Ingestion complete|Runtime\.OutOfMemory|Task timed out|bad doc|validation|unsupported|invalid)/
| stats
  countif(kind="Received event") as received,
  countif(kind="Ingestion complete") as ingestion_complete,
  countif(kind="Runtime.OutOfMemory") as oom,
  countif(kind="Task timed out") as timeout,
  countif(kind="bad doc" or kind="validation" or kind="unsupported" or kind="invalid") as bad_doc
  by documentId
| filter received > 0 and (ingestion_complete + oom + timeout + bad_doc) = 0
| sort documentId asc
```

## Step 5: Triage

- For entries in `missing`: trace by `bundle_reference`, then check queueing/delivery path (SQS DLQ, SNS delivery/filter)
- For entries in `unpublished`: inspect publish path logs for the same `bundle_reference` and `parser_run_id`

## Done Criteria

- `counts.missing = 0`
- `counts.unpublished = 0`
- `unaccounted = 0`

## Optional Local Guardrails

Before operational use, run local logic checks:

```bash
docs/run-parser-run-reconciliation-tests.sh
docs/run-parser-run-reconciliation-fixture-tests.sh
```
