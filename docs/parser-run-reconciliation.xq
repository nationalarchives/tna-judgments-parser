xquery version "1.0-ml";

import module namespace recon = "http://nationalarchives.gov.uk/parser-run-reconciliation" at "parser-run-reconciliation-lib.xqy";
declare namespace dls = "http://marklogic.com/xdmp/dls";

declare variable $parserRunId as xs:string external;
declare variable $expectedBundleReferences as xs:string* external;

declare function local:extract-bundle-reference($doc as node()) as xs:string? {
  let $properties := xdmp:document-properties(fn:base-uri($doc))
  let $annotation-text := string-join(
    for $a in $properties//dls:annotation
    return normalize-space(string($a)),
    " "
  )
  let $match := fn:analyze-string($annotation-text, '"reference"\s*:\s*"([0-9a-fA-F-]{36})"')/fn:match[1]/fn:group[@nr = "1"][1]
  return
    if (exists($match)) then
      fn:lower-case(normalize-space(string($match)))
    else
      ()
};

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
  let $bundle-reference := local:extract-bundle-reference($d)
  where exists($bundle-reference)
  return $bundle-reference
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
  let $bundle-reference := local:extract-bundle-reference($d)
  where exists($bundle-reference)
  return $bundle-reference
)

let $result := recon:result($expectedBundleReferences, $ingested, $published)

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
