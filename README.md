Judgments parser
================

This parser converts UK judgments from .docx format to XML. It is written in C# and requires .NET 5.0.

C# API
------

To invoke the parser programatically, clients should use the classes in the [UK.Gov.NationalArchives.Judgments.Api](./src/api/) namespace.
1. Create a [Request](./src/api/Request.cs) object, with the following properties:
    - Content (required), a byte array, the content of the judgment, in .docx format
    - Filename (optional), a string, the name of .docx file containing the judgment
    - Attachments (optional), an array of [Attachment](./src/api/Request.cs) objects, having the following properties:
        - Content (required), a byte array, the content of the attachment, in .docx format
        - Type (required), an [enum](./src/api/Request.cs), with the following possibe values: Order
        - Filename (optional), a string, the name of .docx file containing the attachment
    - Meta (optional), a [Meta](./src/api/Meta.cs) object, with the following properties:
        - Court (optional), a string, the identifier of the court
        - Cite (optional), a string, the natural citation of the case
        - Date (optional), a [date](https://datatracker.ietf.org/doc/html/rfc3339#section-5.6), the date of the judgment
        - Name (optional), a string, the case name
        - Uri (optional), a string, a URI for the judgment
        - Attachments (optional), an array of [ExternalAttachment](./src/api/Meta.cs) objects, having the following properties:
            - Name (required), a string, the name of the attachment for display
            - Link (optional), a string, a URL for the attachment
    - Hint (optional), an [enum](./src/api/Parser.cs), with the following possibe values: UKSC, UKCA, UKHC, UKUT, Judgment, PressSummary. If present, the parser will attempt to parse a judgment only of the specified type.
2. Pass it to the Parse method in the [Parser](./src/api/Parser.cs) class,
3. Receive a [Response](./src/api/Response.cs) object, which will have the following properties:
    - Xml, a string, the judgment in LegalDocML
    - Images, an array of [Image](./src/api/Response.cs) objects, having the following properties:
        - Content, a byte array, the content of the image
        - Type, a string, the MIME type of the image
        - Name, a string, the name of the image as referred to in the XML
    - Meta, a [Meta](./src/api/Meta.cs) object, as above


REST API
--------

A REST API, mimicking the above, is available at <https://parse.judgments.tna.jurisdatum.com>. Its specification can be found at [/api.yaml](https://parse.judgments.tna.jurisdatum.com/api.yaml).


CLI
---

The parser can also be invoked from the command line, as follows:

    dotnet run --input path/to/file.docx

So, for example, the following command will parse the included test document and direct the output to the console:

    dotnet run --input test/judgments/test1.docx

To direct the XML output to a file, use the `--output` option, like so:

    dotnet run --input test/judgments/test1.docx --output something.xml

To save the XML and all of the embedded images to a .zip file, use the `--output-zip` option, like so:

    dotnet run --input test/judgments/test1.docx --output-zip something.zip

If the `--log` option is used, the parser will log its progress to the specified file. For example:

    dotnet run --input test/judgments/test1.docx --output something.xml --log log.txt

And if the `--test` option is used, the parser will perform a few tests and display the results either in the console or, if logging is enabled, to the log file.
