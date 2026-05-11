xquery version "3.1";

import module namespace recon = "http://nationalarchives.gov.uk/parser-run-reconciliation" at "parser-run-reconciliation-lib.xqy";

declare function local:fixture-doc($documentId as xs:string, $parserRunId as xs:string, $published as xs:boolean) as element(doc) {
  <doc>
    <documentId>{$documentId}</documentId>
    <parser_run_id>{$parserRunId}</parser_run_id>
    <published>{if ($published) then "true" else "false"}</published>
  </doc>
};

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

declare function local:run-fixture($docs as element(doc)*, $parserRunId as xs:string, $expectedIds as xs:string*) as map(*) {
  let $ingested := recon:uniq(
    for $d in $docs[parser_run_id = $parserRunId]
    return string(($d/documentId)[1])
  )
  let $published := recon:uniq(
    for $d in $docs[parser_run_id = $parserRunId and published = "true"]
    return string(($d/documentId)[1])
  )
  let $result := recon:result($expectedIds, $ingested, $published)
  return map {
    "ingested": $ingested,
    "published": $published,
    "result": local:view($parserRunId, $result)
  }
};

declare function local:test($name as xs:string, $parserRunId as xs:string, $expectedIds as xs:string*, $docs as element(doc)*, $expectedResult as map(*)) as xs:string {
  let $actual := local:run-fixture($docs, $parserRunId, $expectedIds)
  let $_log0 := local:log($name, "parserRunId", $parserRunId)
  let $_log1 := local:log($name, "expectedIds", $expectedIds)
  let $_log2 := local:log($name, "fixtureDocsInRun", $docs[parser_run_id = $parserRunId])
  let $_log3 := local:log($name, "ingestedFromFixture", $actual?ingested)
  let $_log4 := local:log($name, "publishedFromFixture", $actual?published)
  let $_log5 := local:log($name, "actual", $actual?result)
  let $_1 := local:assert-eq(concat($name, " counts"), $actual?result?counts, $expectedResult?counts)
  let $_2 := local:assert-eq(concat($name, " missing"), $actual?result?missing, $expectedResult?missing)
  let $_3 := local:assert-eq(concat($name, " unpublished"), $actual?result?unpublished, $expectedResult?unpublished)
  let $_4 := local:assert-eq(concat($name, " published"), $actual?result?published, $expectedResult?published)
  return concat("PASS ", $name)
};

let $docs := (
  local:fixture-doc("A", "run-1", true()),
  local:fixture-doc("B", "run-1", true()),
  local:fixture-doc("C", "run-1", false()),
  local:fixture-doc("E", "run-1", true()),
  local:fixture-doc("Z", "run-2", true()),
  local:fixture-doc("B", "run-1", true()),
  local:fixture-doc("", "run-1", true())
)

return (
  local:test(
    "fixture-flow-run-1",
    "run-1",
    ("A", "B", "C", "D"),
    $docs,
    map {
      "counts": map { "expected": 4, "ingested": 4, "published": 3, "missing": 1, "unpublished": 1 },
      "missing": array { "D" },
      "unpublished": array { "C" },
      "published": array { "A", "B", "E" }
    }
  ),
  local:test(
    "fixture-flow-run-2",
    "run-2",
    ("Z", "Y"),
    $docs,
    map {
      "counts": map { "expected": 2, "ingested": 1, "published": 1, "missing": 1, "unpublished": 0 },
      "missing": array { "Y" },
      "unpublished": array {},
      "published": array { "Z" }
    }
  ),
  "All fixture flow reconciliation tests passed"
)
