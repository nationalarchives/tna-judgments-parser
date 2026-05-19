xquery version "1.0-ml";
import module namespace dls = "http://marklogic.com/xdmp/dls" at "/MarkLogic/dls.xqy";

(: 
   Creates a csv file with details of documents ingested by a specific backlog parser run.
   See bulk-upload-process.md for full details.
:)

(: CHANGE ME to the specific parser run ID you want to look for :)
let $target-run-id := "PUT_PARSER_RUN_ID_HERE"

(: Search for documents last updated by given parser run ID :)
let $results := cts:search(
  collection("http://marklogic.com/collections/dls/latest-version"),
    cts:properties-fragment-query(
    cts:element-value-query(xs:QName("parser-run-id"), $target-run-id)
  )
)

(: Create CSV output :)
let $csv-header := "document_URI,fake_TRE_UUID,published,AWS_request_id"

let $rows :=
  for $doc in $results
    let $uri := xdmp:node-uri($doc)

    let $props := xdmp:document-properties($uri)
    let $published := string($props//published)
    
    let $annotation := xdmp:unquote(string($props//dls:annotation), (), ("format-json"))/node()
    let $fake-tre-uuid := string($annotation/payload/tre_raw_metadata/parameters/TRE/reference)
    let $aws-request-id := string($annotation/payload/aws_lambda_context/aws_request_id)

    (: Construct the CSV row :)
    return fn:concat('"', $uri, '","', $fake-tre-uuid, '","', $published, '","', $aws-request-id, '"')

(: Join each row with a newline character :)
return string-join(($csv-header, $rows), "&#10;")