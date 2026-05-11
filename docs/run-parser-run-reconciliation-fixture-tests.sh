#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"
basex parser-run-reconciliation-fixture-tests.xq
