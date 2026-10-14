param(
    [string]$InputJson = 'docs/balance/balance-data.json',
    [string]$OutputXlsx = 'docs/balance/PaperDefense-Mechanics-Balance.xlsx',
    [string[]]$CurrentEvidence = @()
)
$ErrorActionPreference = 'Stop'
$taskRuntime = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node'
$taskNode = Join-Path $taskRuntime 'bin/node.exe'
$taskModules = Join-Path $taskRuntime 'node_modules'
$taskJunction = Join-Path $PSScriptRoot 'node_modules'
if (-not (Test-Path -LiteralPath $taskNode)) { throw 'Bundled Node.js runtime is unavailable. Run load_workspace_dependencies.' }
if (-not (Test-Path -LiteralPath $taskJunction)) {
    New-Item -ItemType Junction -Path $taskJunction -Target $taskModules | Out-Null
}
& $taskNode (Join-Path $PSScriptRoot 'build.mjs') $InputJson $OutputXlsx @CurrentEvidence
if ($LASTEXITCODE -ne 0) { throw "Workbook builder exited with code $LASTEXITCODE" }
