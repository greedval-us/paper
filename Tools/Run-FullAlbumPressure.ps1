param(
    [string]$PlayerRelativePath = 'Builds\FullAlbumTarget\PaperDefense.exe',
    [string]$ReportTag = 'full-album',
    [ValidateSet(1, 2)][int[]]$Speeds = @(1, 2)
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$playerPath = Join-Path $projectRoot $PlayerRelativePath
if (-not (Test-Path -LiteralPath $playerPath -PathType Leaf)) { throw ('Build the Player before running this measurement: ' + $playerPath) }
if ($ReportTag -notmatch '^[a-z0-9-]+$') { throw 'ReportTag must contain lowercase letters, digits or hyphens.' }
$reportDirectory = Join-Path $projectRoot 'docs\validation'
$runStamp = [DateTime]::UtcNow.ToString('yyyy-MM-dd-HHmmss')
foreach ($speed in $Speeds) {
    $reportPath = Join-Path $reportDirectory ($runStamp + '-' + $ReportTag + '-player-x' + $speed + '.json')
    $logPath = Join-Path $reportDirectory ($runStamp + '-' + $ReportTag + '-player-x' + $speed + '.log')
    if ((Test-Path -LiteralPath $reportPath) -or (Test-Path -LiteralPath $logPath)) { throw 'Refusing to replace an existing measurement.' }
    $playerArguments = @('-paper-pressure', '-paper-memory-only', '-pressure-speed', [string]$speed,
        '-pressure-report', ('"' + $reportPath + '"'), '-logFile', ('"' + $logPath + '"'),
        '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '1000')
    $playerProcess = Start-Process -FilePath $playerPath -ArgumentList $playerArguments -WorkingDirectory (Split-Path -Parent $playerPath) -WindowStyle Hidden -PassThru
    Write-Output ('Started Player x' + $speed + ', PID ' + $playerProcess.Id + ', report ' + $reportPath)
    $deadline = [DateTime]::UtcNow.AddMinutes(11)
    while (-not $playerProcess.WaitForExit(30000)) {
        if ([DateTime]::UtcNow -ge $deadline) { throw ('Player measurement exceeded eleven minutes; PID ' + $playerProcess.Id) }
    }
    $playerProcess.Refresh()
    if (-not (Test-Path -LiteralPath $reportPath)) { throw ('Player exited without its measurement; inspect ' + $logPath) }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    [pscustomobject]@{
        Speed = $speed; ExitCode = $playerProcess.ExitCode; Completed = $report.completed
        Passed = $report.simulation.Passed; Fingerprint = $report.simulation.Fingerprint
        MemoryOnly = $report.workshopProfileMemoryOnly; Frames = $report.allFrames.frames
        AverageMs = $report.allFrames.averageMilliseconds; P95Ms = $report.allFrames.p95Milliseconds
        P99Ms = $report.allFrames.p99Milliseconds; Report = $reportPath
    } | ConvertTo-Json -Compress | Write-Output
    if ($playerProcess.ExitCode -ne 0 -or -not $report.completed -or -not $report.simulation.Passed -or -not $report.workshopProfileMemoryOnly) {
        throw ('Player validation failed; inspect ' + $reportPath)
    }
}
