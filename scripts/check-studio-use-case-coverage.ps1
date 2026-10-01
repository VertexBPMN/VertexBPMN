[CmdletBinding()]
param(
    [string[]]$UseCase,
    [string]$ResultsDirectory,
    [switch]$ListAllTests,
    [switch]$RequireComplete,
    [switch]$AsJson
)

# Read-only source audit. Never builds, starts services or executes a test runner.
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$catalogPath = Join-Path $repositoryRoot 'docs/testing/studio-use-cases.tsv'
$cases = @(Import-Csv -LiteralPath $catalogPath -Delimiter "`t")
foreach ($case in $cases) { if ($case.Remaining -eq '-') { $case.Remaining = '' } }
$issues = [Collections.Generic.List[string]]::new()
$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$sourceCache = @{}

foreach ($case in $cases) {
    if (-not $seen.Add($case.Id)) { $issues.Add("Duplicate use-case ID: $($case.Id)") }
    if ($case.Id -notmatch '^[A-Z0-9][A-Z0-9.\-]+$') { $issues.Add("Invalid ID: $($case.Id)") }
    if ($case.Status -notin @('existing', 'added', 'gap')) { $issues.Add("Invalid status: $($case.Id)") }
    if ($case.Kind -notin @('browser-real', 'browser-oidc', 'browser-stub', 'browser-smoke', 'api-real', 'inventory', 'none')) {
        $issues.Add("Invalid test kind: $($case.Id)")
    }
    if ($case.Status -eq 'gap') {
        if ($case.File -or $case.Method -or $case.Kind -ne 'none' -or -not $case.Remaining) {
            $issues.Add("Gap must name remaining work, not pretend to be implemented: $($case.Id)")
        }
        continue
    }
    if (-not $case.File -or -not $case.Method -or $case.Kind -eq 'none') {
        $issues.Add("Implemented mapping must specify kind, file and method: $($case.Id)")
        continue
    }
    $path = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $case.File))
    $testRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'tests/VertexBPMN.Studio.UiTests')) + [IO.Path]::DirectorySeparatorChar
    if (-not $path.StartsWith($testRoot, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $path)) {
        $issues.Add("Test file missing or outside UI test project: $($case.Id) -> $($case.File)")
        continue
    }
    if (-not $sourceCache.ContainsKey($path)) { $sourceCache[$path] = Get-Content -LiteralPath $path -Raw }
    $pattern = '\bpublic\s+async\s+Task\s+' + [regex]::Escape($case.Method) + '\s*\('
    if ($sourceCache[$path] -notmatch $pattern) { $issues.Add("Test method missing: $($case.Id) -> $($case.Method)") }
}
if ($UseCase) {
    foreach ($id in $UseCase) { if (-not $seen.Contains($id)) { $issues.Add("Unknown requested ID: $id") } }
}
if ($RequireComplete) {
    foreach ($case in $cases | Where-Object { $_.Group -ne 'Testinventar' -and ($_.Status -eq 'gap' -or $_.Remaining) }) {
        $issues.Add("Incomplete acceptance coverage: $($case.Id) -> $($case.Remaining)")
    }
}
if ($issues.Count) { throw ($issues -join [Environment]::NewLine) }

# Inventory includes all declared async test methods, even those not yet mapped
# to a manual use case. It is an inventory, not an assertion of browser coverage.
$inventory = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'tests/VertexBPMN.Studio.UiTests') -Filter '*Tests*.cs' -File |
    Sort-Object Name | ForEach-Object {
        $file = $_
        $source = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($match in [regex]::Matches($source, '\bpublic\s+async\s+Task\s+(?<method>\w+)\s*\(')) {
            $method = $match.Groups['method'].Value
            $relative = [IO.Path]::GetRelativePath($repositoryRoot, $file.FullName).Replace('\', '/')
            $mapped = @($cases | Where-Object { $_.File -eq $relative -and $_.Method -eq $method })
            $line = 1 + ([regex]::Matches($source.Substring(0, $match.Index), "`n")).Count
            [pscustomobject]@{
                File = $relative; Line = $line; Method = $method
                UseCaseIds = @($mapped.Id); Kinds = @($mapped.Kind | Sort-Object -Unique)
                Mapped = $mapped.Count -gt 0
            }
        }
    })
$selected = if ($UseCase) { @($cases | Where-Object { $_.Id -in $UseCase }) } else { $cases }
$acceptanceCases = @($cases | Where-Object Group -ne 'Testinventar')
$summary = [pscustomobject]@{
    Audit = 'static source only; no tests executed; existing/added never means passed'
    AcceptanceUseCases = $acceptanceCases.Count
    InventoryEntries = @($cases | Where-Object Group -eq 'Testinventar').Count
    ExistingMappings = @($acceptanceCases | Where-Object Status -eq 'existing').Count
    AddedMappings = @($acceptanceCases | Where-Object Status -eq 'added').Count
    Unimplemented = @($acceptanceCases | Where-Object Status -eq 'gap').Count
    PartialRequirements = @($acceptanceCases | Where-Object { $_.Status -ne 'gap' -and $_.Remaining }).Count
    DeclaredTestMethods = $inventory.Count
    UnmappedTestMethods = @($inventory | Where-Object { -not $_.Mapped }).Count
}
$evidence = @()
if ($ResultsDirectory) {
    $resultRoot = [IO.Path]::GetFullPath($ResultsDirectory)
    $resultPath = Join-Path $resultRoot 'results.xml'
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "Missing xUnit result: $resultPath" }
    [xml]$results = Get-Content -LiteralPath $resultPath -Raw
    $resultTests = @($results.SelectNodes('//test'))
    $currentCatalogHash = (Get-FileHash -LiteralPath $catalogPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $evidence = @(Get-ChildItem -LiteralPath $resultRoot -Filter 'scenario.json' -Recurse -File | ForEach-Object {
        $artifact = $_
        $metadata = Get-Content -LiteralPath $artifact.FullName -Raw | ConvertFrom-Json
        # Strict display-name matching disambiguates InlineData. Never infer Pass
        # from a screenshot, a method name, or presence of diagnostic metadata.
        $matchingTests = @($resultTests | Where-Object { $_.GetAttribute('name') -eq $metadata.testDisplayName })
        [pscustomobject]@{
            UseCaseIds = @($metadata.useCaseIds)
            Method = $metadata.method; Variant = $metadata.variant
            TestDisplayName = $metadata.testDisplayName
            Result = if ($matchingTests.Count -eq 1) { $matchingTests[0].GetAttribute('result') } else { 'unmatched' }
            Revision = $metadata.revision; DirtyCheckout = $metadata.dirtyCheckout
            CatalogMatchesCurrent = $metadata.catalogSha256 -eq $currentCatalogHash
            AssemblySha256 = $metadata.assemblySha256
            ArtifactDirectory = $artifact.DirectoryName
            HasTrace = Test-Path -LiteralPath (Join-Path $artifact.DirectoryName 'playwright-trace.zip')
            HasScreenshot = Test-Path -LiteralPath (Join-Path $artifact.DirectoryName 'final-page.png')
            Note = 'Historical run result only; not acceptance of the current source or all requirements'
        }
    })
}
if ($AsJson) {
    [pscustomobject]@{ Summary = $summary; Cases = $selected; Inventory = $inventory; Evidence = $evidence } | ConvertTo-Json -Depth 6
} else {
    $summary | Format-List
    $selected | Select-Object Id, Kind, Status, Variant, Scenario, Remaining | Format-Table -Wrap
    if ($ListAllTests) { $inventory | Format-Table File, Line, Method, UseCaseIds -Wrap }
    if ($ResultsDirectory) { $evidence | Format-List }
}
