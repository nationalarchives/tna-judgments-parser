# Parser Run Reconciliation Playbook

> WIP: this is the current general approach, but Steps 1, 2, and 3 must still be tested end-to-end in MarkLogic and CloudWatch before being treated as fully validated runbook procedure.

## Key files

- Canonical MarkLogic query: `docs/parser-run-reconciliation.xq`
- Shared reconciliation logic: `docs/parser-run-reconciliation-lib.xqy`
- Local logic tests: `docs/run-parser-run-reconciliation-tests.sh`
- Local fixture-flow tests: `docs/run-parser-run-reconciliation-fixture-tests.sh`

## External dependencies

- MarkLogic ingestion path (source of stored document shape): [ds-caselaw-ingester](https://github.com/nationalarchives/ds-caselaw-ingester)
- API client used in ingestion flow: [ds-caselaw-custom-api-client](https://github.com/nationalarchives/ds-caselaw-custom-api-client)
- Any field-path assumptions in this playbook (for example `documentId`, `parser_run_id`, `published`) must be validated against what these services persist in MarkLogic.

## Inputs

- parserRunId
- expectedIds (MarkLogic document URIs without `.xml`, for example `uksc/2025/123`)
- CloudWatch time window that covers the full run

## 1) Reconcile in one XQuery

Run `docs/parser-run-reconciliation.xq` and pass `parserRunId` and `expectedIds` as external variables.
The reconciliation logic is implemented in `docs/parser-run-reconciliation-lib.xqy`.

If you only have `expected.txt`, load it first in your client and pass lines as `$expectedIds`.

Definitions:

- missing = expected - ingested
- unpublished = ingested - published
- published = published

Identifier note:

- `docs/parser-run-reconciliation.xq` now uses MarkLogic document URI as the reconciliation key (`fn:base-uri($doc)` with `.xml` suffix removed).
- `published` is read as a MarkLogic document property (not an XML body element), matching `ds-caselaw-custom-api-client` behavior.
- parser run matching uses `dls:annotation` in document properties, where `ds-caselaw-ingester` stores version annotation payloads.

## Local verification scope

- `docs/parser-run-reconciliation-tests.xq` validates the reconciliation set logic only (`expected`, `ingested`, `published` -> `missing`, `unpublished`).
- `docs/parser-run-reconciliation-fixture-tests.xq` validates a MarkLogic-like flow locally by applying parser-run and published filters over fixture documents before reconciliation.
- Neither local test executes MarkLogic `cts:search`. Use MarkLogic execution of `docs/parser-run-reconciliation.xq` for full end-to-end verification.

## 2) CloudWatch accounting

Replace <PARSER_RUN_ID> and run:

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

- unaccounted = received - (ingestion_complete + oom + timeout + bad_doc)

If unaccounted != 0, run the next query.

## 3) Find docs with no terminal outcome

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

## 4) Triage

- missing: SQS DLQ -> SQS metrics (Sent/Received/Deleted) -> SNS metrics (Published/Delivered/Failed) -> SNS filter/policies
- unpublished: CloudWatch by documentId + parser_run_id; check publish exception, timeout, retry-only behavior
- published: no action

## Done criteria

- missing count = 0
- unpublished count = 0
- unaccounted = 0
