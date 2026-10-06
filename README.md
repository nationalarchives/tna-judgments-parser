# Judgments parser

This parser converts UK judgments from .docx format to XML. It is written in C# and requires .NET 10.0.

<!-- TOC -->
* [Judgments parser](#judgments-parser)
  * [Release process](#release-process)
    * [Find Case Law](#find-case-law)
  * [Deployment](#deployment)
    * [Find Case Law](#find-case-law-1)
      * [Validating a deployment](#validating-a-deployment)
  * [Using the parser CLI](#using-the-parser-cli)
  * [Local development](#local-development)
    * [Dev Containers](#dev-containers)
    * [Pre-commit hooks](#pre-commit-hooks)
    * [Tests](#tests)
  * [Other Documentation](#other-documentation)
<!-- TOC -->

## Release process
<!-- last_review: 2026-05-20 -->

### Find Case Law

> [!IMPORTANT]
> This section only covers Find Case Law. If you are trying to create a new release for another project, you should instead follow that project's release process.

1. Update the code
    - Make a new branch for the release
    - Update `version.targets` in the root of the repo with the new version number - this is used by the parser code to add `<uk:parser>x.x.x</uk:parser>` to the parsed xml outputs
    - Push the branch and open a new PR against `main`
    - Merge the PR
1. Create a GitHub Release
    - Create a new tag on `main` with the same version number as `version.targets`
    - Generate release notes
    - Publish the release

## Deployment
<!-- last_review: 2026-05-20 -->

### Find Case Law

> [!IMPORTANT]
> This section only covers Find Case Law. If you are trying to deploy the parser for another project, you should instead follow that project's deployment process.

1. Wait for the next day
    - A [workflow in da-tre-terraform-environments](https://github.com/nationalarchives/da-tre-terraform-environments/actions/workflows/parser_cd.yml) is scheduled to run each night and deploy the latest release.

#### Validating a deployment

1. Go to [Find Case Law](https://caselaw.nationalarchives.gov.uk/) and check that a new judgment has the latest `<uk:parser>` version in it.

## Using the parser CLI

The repo's CLI (`dotnet run --input path/to/file.docx --hint <hint> ...`, run from the repo root) handles legislation and Lawmaker document types only. Judgment parsing (find-caselaw) is not available from this CLI.

The `--hint` option is required and selects the document type: `em`, `en`, `ia`, `tn`, `cop`, `od` for legislation, or a Lawmaker type such as `nipubb`, `uksi`, or `ukprib`. For example:

    dotnet run --hint em --input path/to/file.docx

To direct the XML output to a file, use the `--output` option, like so:

    dotnet run --hint em --input path/to/file.docx --output something.xml

To save the XML and all of the embedded images to a .zip file, use the `--output-zip` option, like so:

    dotnet run --hint em --input path/to/file.docx --output-zip something.zip

If the `--log` option is used, the parser will log its progress to the specified file. For example:

    dotnet run --hint em --input path/to/file.docx --output something.xml --log log.txt

To validate an existing AKN file instead of transforming a .docx, use `--validate-akn`:

    dotnet run --validate-akn --input path/to/file.akn

## Local development

### Dev Containers

You can run this code in a Dev Container in VSCode or [other IDES](https://containers.dev/supporting).

* Install the `ms-vscode-remote.remote-containers` extension

* Press F1, and select `Dev Containers: Open Folder in Container...`. Select the parser folder.

You can now run tests in debug mode from the Flask (Testing) icon on the left.

Configuration lives in `devcontainer.json`.

### Pre-commit hooks

We use [Husky .Net](https://alirezanet.github.io/Husky.Net/) for pre-commit hooks. It is versioned in `dotnet-tools.json` and is set to configure itself automatically upon build via `Directory.Build.targets`.

To lint staged files before commit use:

```bash
dotnet husky run --name "dotnet-format"
```

To add or amend pre-commit hooks, edit `.husky/task-runner.json`

### Tests

There are a mixture of unit, integration and end to end tests which overall give a good coverage of the codebase. These run in CI and should be updated/added to when changes are made.

To run all the tests use your IDE or run: 

```shell
dotnet test tna-judgments-parser.sln
```

When significant changes are made to the parser some tests may fail due to differences in the expected xml output. The test xmls can be updated en masse by running:

```shell
dotnet test tna-judgments-parser.sln --filter test.UpdateXmlFiles.UpdateJudgmentXmls -e UPDATE_XML="true"
```

## Other Documentation

- See [Backlog readme](./backlog/README.md) for bulk parse documentation.
- See [Numbering3 docs](docs/numbering3.md) for information on the paragraph numbering algorithm.
- See [Find Caselaw ADRs](https://github.com/nationalarchives/ds-find-caselaw-docs/tree/main/doc/adr) for contextual information on some Parser decisions.
