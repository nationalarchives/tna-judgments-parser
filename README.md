Judgments parser
================

This parser converts UK judgments from .docx format to XML. It is written in C# and requires .NET 8.0.

<!-- TOC -->
* [Judgments parser](#judgments-parser)
  * [Making a FCL release](#making-a-fcl-release)
  * [Using the parser](#using-the-parser)
    * [C# API](#c-api)
    * [REST API](#rest-api)
    * [CLI](#cli)
  * [Tests](#tests)
<!-- TOC -->

## Making a FCL release

1. Update the code
    - Make a branch
    - Update `version.targets` in the root of the repo with the new version number - this is used by the parser code to add `<uk:parser>x.x.x</uk:parser>` to the parsed xml outputs
    - Merge to main
2. Create a GitHub Release
    - Create a new Tag with the same version number as `version.targets`
    - Generate release notes
    - Publish release
3. Wait for the next day
    - A [workflow in da-tre-terraform-environments](https://github.com/nationalarchives/da-tre-terraform-environments/actions/workflows/parser_cd.yml) is scheduled to run each night and deploy the latest release
    - Go to [Find Case Law](https://caselaw.nationalarchives.gov.uk/) and check that a new judgment has the new `<uk:parser>` in it

## Using the parser

### C# API

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


### REST API

A REST API, mimicking the above, is available at <https://parse.judgments.tna.jurisdatum.com>. Its specification can be found at [/api.yaml](https://parse.judgments.tna.jurisdatum.com/api.yaml).


### CLI

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

## Tests

There are a mixture of unit, integration and end to end tests which overall give a good coverage of the codebase. These run in CI and should be updated/added to when changes are made.

To run all the tests use your IDE or run: 

```shell
dotnet test tna-judgments-parser.sln
```

When significant changes are made to the parser some tests may fail due to differences in the expected xml output. The test xmls can be updated en masse by running:

```shell
dotnet test tna-judgments-parser.sln --filter test.UpdateXmlFiles.UpdateJudgmentXmls -e UPDATE_XML="true"
```
