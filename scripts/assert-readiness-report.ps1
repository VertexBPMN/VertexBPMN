function Assert-ReadinessReport {
    param([Parameter(Mandatory)][string]$Path, [string[]]$RequiredMethods = @())
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing test report: $Path" }
    [xml]$report = Get-Content -LiteralPath $Path -Raw
    $assemblies = @($report.SelectNodes('/assemblies/assembly'))
    $tests = @($report.SelectNodes('/assemblies/assembly/collection/test'))
    if ($assemblies.Count -eq 0 -or $tests.Count -eq 0) { throw "No tests in $Path" }
    $declared = ($assemblies | Measure-Object -Property total -Sum).Sum
    if ($declared -ne $tests.Count) { throw "Incomplete test report: $Path" }
    foreach ($assembly in $assemblies) {
        if ([int]$assembly.failed -gt 0 -or [int]$assembly.skipped -gt 0 -or [int]$assembly.errors -gt 0) {
            throw "Failed, skipped or errored tests in $Path"
        }
    }
    if (@($tests | Where-Object { $_.result -ne 'Pass' }).Count -gt 0) { throw "Non-passing test in $Path" }
    foreach ($method in $RequiredMethods) {
        if (@($tests | Where-Object { $_.method -eq $method }).Count -eq 0) { throw "Required test missing: $method" }
    }
    return $tests.Count
}
