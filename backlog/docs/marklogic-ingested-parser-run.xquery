xquery version "1.0-ml";
import module namespace dls = "http://marklogic.com/xdmp/dls" at "/MarkLogic/dls.xqy";

(: 
   Creates a csv file with details of documents ingested by a specific backlog parser run.
   See bulk-upload-process.md for full details.
:)

(: CHANGE ME to the specific parser run ID you want to look for :)
let $target-run-id := "PUT_PARSER_RUN_ID_HERE"

(: Create CSV output :)
let $csv-header := "document_URI,fake_TRE_UUID,published,AWS_request_id"

let $rows :=
  for $uri in cts:uris("", (), cts:and-query(
    (
      cts:collection-query("http://marklogic.com/collections/dls/latest-version"),
      cts:collection-query("judgment"),
      cts:properties-fragment-query(cts:element-value-query(xs:QName("parser-run-id"), $target-run-id))
    )))

    (: Get published data from latest version :)
    let $props := xdmp:document-properties($uri)
    let $published := string($props//published)

    (: Get annotation data from the version which has this parserrun id in annotation too - it won't be in the latest version for enriched documents and won't be in the first version for re-run documents :)
    let $versioned-uris := fn:replace($uri, "^/(d-[^/]+)\.xml$", "/$1_xml_versions/*.xml")
    let $version-with-annotation := cts:uri-match($versioned-uris, (), cts:properties-fragment-query(cts:element-query(xs:QName("dls:annotation"), $target-run-id)))

    let $annotation-str := xdmp:document-properties($version-with-annotation)//dls:annotation/string()
    let $annotation := xdmp:unquote($annotation-str, (), "format-json")/node()

    let $fake-tre-uuid := $annotation/payload/tre_raw_metadata/parameters/TRE/reference/string()
    let $aws-request-id := $annotation/payload/aws_lambda_context/aws_request_id/string()

    (: Construct the CSV row :)
    return fn:concat('"', $uri, '","', $fake-tre-uuid, '","', $published, '","', $aws-request-id, '"')

(: Join each row with a newline character :)
return string-join(($csv-header, $rows), "&#10;")
