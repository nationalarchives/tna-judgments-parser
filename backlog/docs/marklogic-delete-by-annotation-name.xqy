xquery version "1.0-ml";
import module namespace dls = "http://marklogic.com/xdmp/dls" at "/MarkLogic/dls.xqy";

(: 
   Deletes documents created by a previous run of the backlog parser. Do not use in production.
   See bulk-upload-process.md for full details.

   Do not run this script with either destructive block uncommented until the
   matching documents have been verified. To check whether a document is
   checked out, replace the final `return xdmp:node-uri($doc)` with the
   commented block below that prints which documents which are checked out.

   If any documents are checked out, uncomment the break-checkout block and run
   the script. Review the results, then uncomment the document-delete block and
   run it again. Keep both blocks commented until the matching documents have
   been verified.
:)

let $csv-filename := "PUT_METADATA_CSV_NAME_HERE.csv"

(: Step 1: Find docs in the judgment collection imported via this CSV, via their DLS annotation :)
let $csv-docs := cts:search(
  collection("judgment"),
  cts:properties-fragment-query(
    cts:element-word-query(xs:QName("dls:annotation"), fn:concat('"name": "', $csv-filename, '"'))
  )
)

(: Step 2: Extract distinct parser-run-id values — first from the direct property,
   falling back to the parser_run_id field inside the dls:annotation JSON payload :)
let $run-ids := fn:distinct-values(
  for $doc in $csv-docs
  let $props := xdmp:document-properties(xdmp:node-uri($doc))
  let $direct-run-id := $props//parser-run-id/text()
  return
    if (fn:exists($direct-run-id)) then
      $direct-run-id
    else
      for $annotation in $props//dls:annotation/text()
      let $parsed := xdmp:from-json-string($annotation)
      let $parser-params := map:get(map:get(map:get(map:get($parsed, "payload"), "tre_raw_metadata"), "parameters"), "PARSER")
      where fn:exists($parser-params)
      return map:get($parser-params, "parser_run_id")
)

let $deep := fn:true()

(: Step 3: Find all judgment docs with any of those run IDs :)
let $result := cts:search(
  collection("judgment"),
  cts:properties-fragment-query(
    cts:element-value-query(xs:QName("parser-run-id"), $run-ids)
  )
)

return (
  fn:concat("Run IDs found: ", fn:string-join($run-ids, ", ")),
  for $doc in $result

    (: Uncomment to print documents which are checked out.
    let $uri := xdmp:node-uri($doc)
    let $checkout-status := dls:document-checkout-status($uri)
    where fn:string-length(fn:string($checkout-status)) gt 0
    return fn:concat($uri, " checked out: ", $checkout-status)
    :)

    (: Uncomment to break checkout before deleting checked-out documents.
    let $uri := xdmp:node-uri($doc)
    where dls:document-checkout-status($uri)
    return dls:break-checkout($uri, $deep)
    :)

    (: Uncomment to delete the matching documents.
    return dls:document-delete(xdmp:node-uri($doc), fn:false(), fn:false())
    :)

    return xdmp:node-uri($doc)
)
