Judgments parser
================

This parser converts judgments from .docx format to XML. It requires Microsoft's .NET Framework, and can be invoked with the following command:

    dotnet run --input path/to/file.docx

So, for example, the following command will parse the included test document and direct the output to the console:

    dotnet run --input test.docx

To direct the output to a file, use the `--output` option, like so:

    dotnet run --input test.docx --output test.xml

If the `--log` option is used, the parser will log its progress to the specified file. For example:

    dotnet run --input test.docx --output test.xml --log log.txt

And if the `--test` option is used, the parser will perform a few tests and display the results either in the console or, if logging is enabled, to the log file.
