xquery version "1.0";

module namespace recon = "http://nationalarchives.gov.uk/parser-run-reconciliation";

declare function recon:uniq($values as xs:string*) as xs:string* {
  distinct-values(for $v in $values return normalize-space($v))[. ne ""]
};

declare function recon:result(
  $expectedIds as xs:string*,
  $ingestedIds as xs:string*,
  $publishedIds as xs:string*
) as element(reconciliation) {
  let $expected := recon:uniq($expectedIds)
  let $ingested := recon:uniq($ingestedIds)
  let $published := recon:uniq($publishedIds)
  let $missing := $expected[not(. = $ingested)]
  let $unpublished := $ingested[not(. = $published)]
  return
    <reconciliation>
      <counts
        expected="{count($expected)}"
        ingested="{count($ingested)}"
        published="{count($published)}"
        missing="{count($missing)}"
        unpublished="{count($unpublished)}"
      />
      <missing>{for $id in $missing return <id>{$id}</id>}</missing>
      <unpublished>{for $id in $unpublished return <id>{$id}</id>}</unpublished>
      <published>{for $id in $published return <id>{$id}</id>}</published>
    </reconciliation>
};
