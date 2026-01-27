<#
.DESCRIPTION
Utility script for assisting Lawmaker developers. Allows convenient running, comparison
and modification of Lawmaker tests.
#>
[CmdletBinding(DefaultParameterSetName = 'Path')]
param(

    [Parameter(
        Mandatory,
        ParameterSetName = 'all',
        HelpMessage="If set, run all tests and diff one at a time. The next diff is not `
        run until the current diff process has terminated. This is much slower than running `
        the tests via dotnet"
    )]
    [switch] $all,

    [Parameter(
        Mandatory,
        ParameterSetName = 'single',
        HelpMessage="The lawmaker test file to run relative to the test/lawmaker directory `
        e.g. ./lm-run.ps1 nipubb/test1",
        Position=0
    )]
    [string] $TestFile,

    [Parameter(ParameterSetName = 'single')]
    [Parameter(ParameterSetName = 'all')]
    [Parameter(HelpMessage="If set, keeps the generated output files at `
    .ignore/<doctype>/<testname>.out.xml")]
    [switch] $keep

)

function Cmp {
    param(
        $Actual,
        $Expected
    )
    if ($null -eq (Get-Command "winmergeu" -ErrorAction SilentlyContinue)) {
        Write-Host "WinMergeU executable not found on PATH. Please install`n
        via scoop: scoop install winmerge`n
        or choco: choco install winmerge`n
        or downloading via https://winmerge.org/ and ensure winmergeu is on your PATH"
    }
    if (-not (Test-Path -Path $Actual)) {
        throw "Could not find $Actual"
    }
    if (-not (Test-Path -Path $Expected)) {
        throw "Could not find $Expected"
    }

    $DiffExitCode = (start-process -WindowStyle Hidden -Wait -PassThru -FilePath winmergeu -ArgumentList "/enableexitcode /noninteractive $Expected $Actual").ExitCode

    # WinMerge exit codes defined here: https://manual.winmerge.org/en/Command_line.html
    if ($DiffExitCode -eq 0) {
        Write-Host "$Expected and $Actual match!"
    } elseif ($DiffExitCode -eq 1) {
        Write-Host "Comparing Actual: $Actual`nwith Expected: $Expected"
        (start-process -Wait -PassThru -FilePath winmergeu -ArgumentList "$Expected $Actual")
    } else {
        Write-Host "Couldn't find compare files"
    }
}

function Run {
    param(
        $FileInput,
        $Output,
        $Hint
    )
    Write-Host "FileInput: $FileInput`nOutput: $Output`nHint: $Hint"
    $CopiedPath = "$FileInput.copy"
    try {
        Copy-Item $FileInput -Destination $CopiedPath
        Set-Variable Logging__LogLevel__Microsoft=Information
        dotnet.exe run --property WarningLevel=0 --input "$CopiedPath" --output "$Output" --log "log.log" --hint $Hint --language "cy" "en"
    } finally {
        Remove-Item $CopiedPath
    }
}

function Test {
    param(
        [string]$TestFile,
        $OutDir

    )
    if (-not(Test-Path "$TestFile.docx")) {
        Write-Host "$TestFile.docx does not exist"
        Exit(1)
    }

    $File = Get-Item "$TestFile.docx"
    $OutFile = $OutDir + $File.BaseName + ".out.xml"
    if (-not(Test-Path -Path $OutFile)) {
        New-Item -Force $OutFile
    }
    Run -FileInput "$TestFile.docx" -Output "$OutFile" -Hint (Get-Item "$TestFile.xml").Directory.Name;
    if ($LASTEXITCODE -eq 0) {
        Cmp -Actual $OutFile -Expected "$TestFile.xml"
    }

    if (-not $keep) {
        Remove-Item $OutFile
    }
}

$OutDir = ".ignore\"
if ($all) {
    $hash = Get-ChildItem -Path "test/lawmaker" -Recurse  -Include "*.xml","*.docx" -Exclude "_*" |
        Group-Object -Property {$_.DirectoryName + "\" + $_.BaseName}
    $hash |
    Where-Object { $_.Count -eq 2 } |
    ForEach-Object {
        Write-Host "============================================================================="
        $file = $_.Group[0].DirectoryName + "\" + $_.Group[0].BaseName
        Test -TestFile $file -OutDir $OutDir
    }

    return
} else {
    Test -TestFile "test/lawmaker/$TestFile" -OutDir $OutDir
}