xquery version "1.0-ml";
import module namespace dls = "http://marklogic.com/xdmp/dls" at "/MarkLogic/dls.xqy";

(: 
   Deletes documents created by a previous run of the backlog parser. Do not use in production.
   See bulk-upload-process.md for full details.

   Do not run this script with either destructive block uncommented until the
   matching documents have been verified. To check whether a document is
   checked out, replace the final `return $uri` with the commented block below
   that prints which documents which are checked out.

   If any documents are checked out, uncomment the break-checkout block and run
   the script. Review the results, then uncomment the document-delete block and
   run it again. Keep both blocks commented until the matching documents have
   been verified.
:)

(: CHANGE ME to the specific parser run ID you want to look for :)
let $target-run-id := "PUT_PARSER_RUN_ID_HERE"

(: Create CSV output :)
let $csv-header := "document_URI,fake_TRE_UUID,published,AWS_request_id"
let $deep := fn:true()
return (
  for $uri in cts:uris("", (), cts:and-query(
    (
      cts:collection-query("http://marklogic.com/collections/dls/latest-version"),
      cts:collection-query("judgment"),
      cts:properties-fragment-query(cts:element-value-query(xs:QName("parser-run-id"), $target-run-id))
    )))

    (: Uncomment to print documents which are checked out.
    let $checkout-status := dls:document-checkout-status($uri)
    where fn:string-length(fn:string($checkout-status)) gt 0
    return fn:concat($uri, " checked out: ", $checkout-status)
    :)

    (: Uncomment to break checkout before deleting checked-out documents.
    where dls:document-checkout-status($uri) 
    return dls:break-checkout($uri, $deep)
    :)
    
    (: Uncomment to delete the matching documents.
    return dls:document-delete($uri, fn:false(), fn:false())
    :)

    return $uri
)
