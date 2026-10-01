[CmdletBinding()]
param(
    [string]$EnvFile = (Join-Path $PSScriptRoot '.env'),
    [string]$Project = 'vertexbpmn-wslc',
    [string]$ComposeSource = $env:WSLC_COMPOSE_SOURCE,
    [string]$Python = 'python',
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ComposeArguments = @('up', '-d', '--build', '--force-recreate', '--wait', '--wait-timeout', '600')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $EnvFile -PathType Leaf)) { throw 'Copy .env.example to .env and set local secrets first.' }
if (!$ComposeSource) {
    $siblingSource = Join-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) 'wslc-compose/src'
    if (Test-Path -LiteralPath $siblingSource -PathType Container) { $ComposeSource = $siblingSource }
}
$adapterArguments = @((Join-Path $PSScriptRoot 'wslc-compose-compat.py'))
if ($ComposeSource) { $adapterArguments += @('--compose-source', $ComposeSource) }
$adapterArguments += @('-f', (Join-Path $PSScriptRoot 'docker-compose.yml'), '-f', (Join-Path $PSScriptRoot 'wslc.override.yml'), '--env-file', $EnvFile, '-p', $Project)
$adapterArguments += $ComposeArguments
$passwordLine = Get-Content -LiteralPath $EnvFile | Where-Object { $_ -match '^\s*RABBITMQ_PASSWORD\s*=' } | Select-Object -Last 1
$rabbitPassword = $env:RABBITMQ_PASSWORD
if (!$rabbitPassword -and $passwordLine) { $rabbitPassword = $passwordLine.Split('=', 2)[1].Trim().Trim('"', "'") }
if (!$rabbitPassword) { throw 'RABBITMQ_PASSWORD is required.' }
$previousEncodedPassword = $env:WSLC_RABBITMQ_PASSWORD_URI
$previousEncodedUser = $env:WSLC_RABBITMQ_USER_URI
$userLine = Get-Content -LiteralPath $EnvFile | Where-Object { $_ -match '^\s*RABBITMQ_USER\s*=' } | Select-Object -Last 1
$rabbitUser = $env:RABBITMQ_USER
if (!$rabbitUser -and $userLine) { $rabbitUser = $userLine.Split('=', 2)[1].Trim().Trim('"', "'") }
if (!$rabbitUser) { $rabbitUser = 'vertexbpmn' }
try {
    $env:WSLC_RABBITMQ_PASSWORD_URI = [Uri]::EscapeDataString($rabbitPassword)
    $env:WSLC_RABBITMQ_USER_URI = [Uri]::EscapeDataString($rabbitUser)
    & $Python @adapterArguments
    if ($LASTEXITCODE -ne 0) { throw "WSLC Compose failed with exit $LASTEXITCODE" }
}
finally {
    $env:WSLC_RABBITMQ_PASSWORD_URI = $previousEncodedPassword
    $env:WSLC_RABBITMQ_USER_URI = $previousEncodedUser
}
