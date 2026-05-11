xquery version "3.1";

import module namespace recon = "http://nationalarchives.gov.uk/parser-run-reconciliation" at "parser-run-reconciliation-lib.xqy";

declare function local:counts($result as element(reconciliation)) as map(*) {
  map {
    "expected": xs:integer($result/counts/@expected),
    "ingested": xs:integer($result/counts/@ingested),
    "published": xs:integer($result/counts/@published),
    "missing": xs:integer($result/counts/@missing),
    "unpublished": xs:integer($result/counts/@unpublished)
  }
};

declare function local:ids($nodes as element(id)*) as array(*) {
  array { for $node in $nodes return string($node) }
};

declare function local:view($parserRunId as xs:string, $result as element(reconciliation)) as map(*) {
  map {
    "parserRunId": $parserRunId,
    "counts": local:counts($result),
    "missing": local:ids($result/missing/id),
    "unpublished": local:ids($result/unpublished/id),
    "published": local:ids($result/published/id)
  }
};

declare function local:log($name as xs:string, $label as xs:string, $value as item()*) as empty-sequence() {
  let $serialized :=
    if ($value instance of map(*) or $value instance of array(*)) then
      serialize($value, map { "method": "json", "indent": true() })
    else if (exists($value) and (every $item in $value satisfies $item instance of element())) then
      serialize(<items>{$value}</items>, map { "method": "xml", "indent": true() })
    else
      string-join(for $item in $value return string($item), ", ")
  let $ignored := trace($serialized, concat("[", $name, "] ", $label, ": "))
  return ()
};

declare function local:assert-eq($label as xs:string, $actual as item()*, $expected as item()*) as empty-sequence() {
  if (deep-equal($actual, $expected)) then
    ()
  else
    error(xs:QName("ASSERT"), concat($label, " expected ", serialize($expected, map { "method": "json" }), " but got ", serialize($actual, map { "method": "json" })))
};

declare function local:test($name as xs:string, $parserRunId as xs:string, $expectedIds as xs:string*, $ingestedIds as xs:string*, $publishedIds as xs:string*, $expectedResult as map(*)) as xs:string {
  let $actual := recon:result($expectedIds, $ingestedIds, $publishedIds)
  let $actualView := local:view($parserRunId, $actual)
  let $_log0 := local:log($name, "parserRunId", $parserRunId)
  let $_log1 := local:log($name, "expectedIds", $expectedIds)
  let $_log2 := local:log($name, "ingestedIds", $ingestedIds)
  let $_log3 := local:log($name, "publishedIds", $publishedIds)
  let $_log4 := local:log($name, "actual", $actualView)
  let $_1 := local:assert-eq(concat($name, " counts"), $actualView?counts, $expectedResult?counts)
  let $_2 := local:assert-eq(concat($name, " missing"), $actualView?missing, $expectedResult?missing)
  let $_3 := local:assert-eq(concat($name, " unpublished"), $actualView?unpublished, $expectedResult?unpublished)
  let $_4 := local:assert-eq(concat($name, " published"), $actualView?published, $expectedResult?published)
  return concat("PASS ", $name)
};

(
  local:test(
    "perfect-run",
    "test-perfect-run",
    ("A", "B", "C"),
    ("A", "B", "C"),
    ("A", "B", "C"),
    map {
      "counts": map { "expected": 3, "ingested": 3, "published": 3, "missing": 0, "unpublished": 0 },
      "missing": array {},
      "unpublished": array {},
      "published": array { "A", "B", "C" }
    }
  ),
  local:test(
    "missing-doc",
    "test-missing-doc",
    ("A", "B", "C", "D"),
    ("A", "B", "C"),
    ("A", "B", "C"),
    map {
      "counts": map { "expected": 4, "ingested": 3, "published": 3, "missing": 1, "unpublished": 0 },
      "missing": array { "D" },
      "unpublished": array {},
      "published": array { "A", "B", "C" }
    }
  ),
  local:test(
    "unpublished-doc",
    "test-unpublished-doc",
    ("A", "B", "C"),
    ("A", "B", "C"),
    ("A", "B"),
    map {
      "counts": map { "expected": 3, "ingested": 3, "published": 2, "missing": 0, "unpublished": 1 },
      "missing": array {},
      "unpublished": array { "C" },
      "published": array { "A", "B" }
    }
  ),
  local:test(
    "dedupe-trim-blanks",
    "test-dedupe-trim-blanks",
    ("A", "A", " B ", "", "  "),
    ("A", "B", "B", " "),
    ("A"),
    map {
      "counts": map { "expected": 2, "ingested": 2, "published": 1, "missing": 0, "unpublished": 1 },
      "missing": array {},
      "unpublished": array { "B" },
      "published": array { "A" }
    }
  ),
  local:test(
    "extra-ingested-doc",
    "test-extra-ingested-doc",
    ("A", "B"),
    ("A", "B", "X"),
    ("A", "B", "X"),
    map {
      "counts": map { "expected": 2, "ingested": 3, "published": 3, "missing": 0, "unpublished": 0 },
      "missing": array {},
      "unpublished": array {},
      "published": array { "A", "B", "X" }
    }
  ),
  "All reconciliation logic tests passed"
)
