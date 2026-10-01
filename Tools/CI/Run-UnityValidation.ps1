param(
    [string]$UnityPath = $env:UNITY_PATH,
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$OutputPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path 'Artifacts\CI')
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($UnityPath) -or -not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw 'Set the full Unity.exe path with -UnityPath or UNITY_PATH.'
}

$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

function Invoke-UnityStep {
    param(
        [string]$Name,
        [string[]]$Arguments
    )

    Write-Host "Running Unity validation: $Name"
    $nativeArguments = foreach ($argument in $Arguments) {
        if ($argument -match '\s') { '"' + $argument.Replace('"', '\"') + '"' } else { $argument }
    }
    $process = Start-Process -FilePath $UnityPath -ArgumentList $nativeArguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        throw "Unity validation failed: $Name (exit code $($process.ExitCode))."
    }
}

function Assert-TestResult {
    param(
        [string]$Name,
        [string]$ResultPath
    )

    if (-not (Test-Path -LiteralPath $ResultPath -PathType Leaf)) {
        throw "Test results were not generated: $ResultPath"
    }

    [xml]$document = Get-Content -LiteralPath $ResultPath
    $run = $document.'test-run'
    if ($null -eq $run -or $run.result -ne 'Passed' -or [int]$run.total -le 0) {
        throw "$Name failed or discovered no tests. Result: $($run.result); total: $($run.total); failed: $($run.failed)."
    }

    Write-Host "$Name passed: $($run.passed)/$($run.total)"
}

$editResults = Join-Path $OutputPath 'editmode-results.xml'
$editLog = Join-Path $OutputPath 'editmode.log'
Invoke-UnityStep 'EditMode tests' @(
    '-batchmode', '-nographics',
    '-projectPath', $ProjectPath,
    '-runTests', '-testPlatform', 'EditMode',
    '-testResults', $editResults,
    '-logFile', $editLog
)
Assert-TestResult 'EditMode tests' $editResults

$playResults = Join-Path $OutputPath 'playmode-results.xml'
$playLog = Join-Path $OutputPath 'playmode.log'
Invoke-UnityStep 'PlayMode tests' @(
    '-batchmode', '-nographics',
    '-projectPath', $ProjectPath,
    '-runTests', '-testPlatform', 'PlayMode',
    '-testResults', $playResults,
    '-logFile', $playLog
)
Assert-TestResult 'PlayMode tests' $playResults

$healthLog = Join-Path $OutputPath 'project-health.log'
Invoke-UnityStep 'project health check' @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', $ProjectPath,
    '-executeMethod', 'ProjectHealthValidator.ValidateOrThrow',
    '-logFile', $healthLog
)

Write-Host "All Unity validations passed. Reports: $OutputPath"
