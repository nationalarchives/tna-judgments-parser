xquery version "1.0-ml";

import module namespace recon = "http://nationalarchives.gov.uk/parser-run-reconciliation" at "parser-run-reconciliation-lib.xqy";
declare namespace dls = "http://marklogic.com/xdmp/dls";

declare variable $parserRunId as xs:string external;
declare variable $expectedIds as xs:string* external;

let $latest-version-query := cts:collection-query("http://marklogic.com/collections/dls/latest-version")

(:
  ds-caselaw-ingester stores parser metadata inside version annotation payloads.
  We therefore match parserRunId against dls:annotation in document properties.
:)
let $parser-run-query := cts:properties-fragment-query(
  cts:element-word-query(xs:QName("dls:annotation"), $parserRunId)
)

(: published is a MarkLogic document property, set via set_boolean_property/set_published in ds-caselaw-custom-api-client :)
let $published-property-query := cts:properties-fragment-query(
  cts:element-value-query(xs:QName("published"), "true")
)

let $ingested := recon:uniq(
  for $d in cts:search(
    fn:collection(),
    cts:and-query((
      $latest-version-query,
      $parser-run-query
    ))
  )
  return substring-before(fn:base-uri($d), ".xml")
)

let $published := recon:uniq(
  for $d in cts:search(
    fn:collection(),
    cts:and-query((
      $latest-version-query,
      $parser-run-query,
      $published-property-query
    ))
  )
  return substring-before(fn:base-uri($d), ".xml")
)

let $result := recon:result($expectedIds, $ingested, $published)

return object-node {
  "parserRunId": $parserRunId,
  "counts": object-node {
    "expected": xs:integer($result/counts/@expected),
    "ingested": xs:integer($result/counts/@ingested),
    "published": xs:integer($result/counts/@published),
    "missing": xs:integer($result/counts/@missing),
    "unpublished": xs:integer($result/counts/@unpublished)
  },
  "missing": json:to-array(for $id in $result/missing/id return string($id)),
  "unpublished": json:to-array(for $id in $result/unpublished/id return string($id)),
  "published": json:to-array(for $id in $result/published/id return string($id))
}
