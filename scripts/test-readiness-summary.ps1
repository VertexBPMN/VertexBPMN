$ErrorActionPreference = 'Stop'
# Load only the reporting function: never start infrastructure or a release run.
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot 'test-production-readiness.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
$function = $ast.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Add-SuiteResult'
}, $true)
if (-not $function) { throw 'Summary function not found.' }
. ([scriptblock]::Create($function.Extent.Text))
$temp = Join-Path ([IO.Path]::GetTempPath()) "readiness-summary-$([Guid]::NewGuid().ToString('N')).xml"
$summary = @{ Stages = @() }
try {
    Set-Content -LiteralPath $temp -Value '<assemblies><assembly total="3" passed="1" failed="1" skipped="1" errors="0" /></assemblies>'
    Add-SuiteResult 'failed' $temp ([DateTime]::UtcNow.AddSeconds(-1)) $false
    $stage = $summary.Stages[-1]
    if ($stage.Status -ne 'Failed' -or $stage.Total -ne 3 -or $stage.Passed -ne 1 -or
        $stage.Failed -ne 1 -or $stage.Skipped -ne 1 -or $stage.Errors -ne 0 -or $stage.DurationSeconds -lt 1) {
        throw 'Failed suite statistics or duration were lost.'
    }
    Set-Content -LiteralPath $temp -Value '<assemblies><assembly total="2" passed="2" failed="0" skipped="0" errors="0" /></assemblies>'
    Add-SuiteResult 'passed' $temp ([DateTime]::UtcNow) $true
    if ($summary.Stages[-1].Status -ne 'Passed' -or $summary.Stages[-1].Passed -ne 2) {
        throw 'Passing suite statistics were lost.'
    }
    Remove-Item -LiteralPath $temp
    Add-SuiteResult 'missing' $temp ([DateTime]::UtcNow) $false
    if ($summary.Stages[-1].Status -ne 'Failed' -or $null -ne $summary.Stages[-1].Failed -or
        -not $summary.Stages[-1].ReportError) { throw 'Missing report was reported as zero failures.' }
    Write-Host '3 summary checks passed.'
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp }
}
