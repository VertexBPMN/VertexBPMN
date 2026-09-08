$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'assert-readiness-report.ps1')
$temp = Join-Path ([IO.Path]::GetTempPath()) "readiness-report-$([Guid]::NewGuid().ToString('N')).xml"
try {
    $valid = '<assemblies><assembly total="1" failed="0" skipped="0" errors="0"><collection><test method="RequiredCase" result="Pass" /></collection></assembly></assemblies>'
    $cases = @(
        @{ Xml = $valid; Required = 'RequiredCase'; Pass = $true },
        @{ Xml = $valid; Required = 'MissingCase'; Pass = $false },
        @{ Xml = $valid.Replace('result="Pass"', 'result="Skip"'); Required = 'RequiredCase'; Pass = $false },
        @{ Xml = $valid.Replace('errors="0"', 'errors="1"'); Required = 'RequiredCase'; Pass = $false },
        @{ Xml = '<assemblies />'; Required = 'RequiredCase'; Pass = $false },
        @{ Xml = '<broken'; Required = 'RequiredCase'; Pass = $false }
    )
    foreach ($case in $cases) {
        Set-Content -LiteralPath $temp -Value $case.Xml
        $accepted = $false
        try { $null = Assert-ReadinessReport $temp @($case.Required); $accepted = $true } catch { }
        if ($accepted -ne $case.Pass) { throw 'Report gate accepted an invalid report or rejected a valid one.' }
    }
    Remove-Item -LiteralPath $temp
    $accepted = $false
    try { $null = Assert-ReadinessReport $temp; $accepted = $true } catch { }
    if ($accepted) { throw 'Missing report was accepted.' }
    Write-Host '7 report-gate checks passed.'
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp }
}
