$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$playerPath = Join-Path $projectRoot 'Builds\FullAlbumTarget\PaperDefense.exe'
if (-not (Test-Path -LiteralPath $playerPath -PathType Leaf)) { throw 'Build FullAlbumTarget before running this measurement.' }
$reportDirectory = Join-Path $projectRoot 'docs\validation'
$runStamp = [DateTime]::UtcNow.ToString('yyyy-MM-dd-HHmmss')
foreach ($speed in @(1, 2)) {
    $reportPath = Join-Path $reportDirectory ($runStamp + '-full-album-player-x' + $speed + '.json')
    $logPath = Join-Path $reportDirectory ($runStamp + '-full-album-player-x' + $speed + '.log')
    if ((Test-Path -LiteralPath $reportPath) -or (Test-Path -LiteralPath $logPath)) { throw 'Refusing to replace an existing measurement.' }
    $playerArguments = @('-paper-pressure', '-paper-memory-only', '-pressure-speed', [string]$speed,
        '-pressure-report', ('"' + $reportPath + '"'), '-logFile', ('"' + $logPath + '"'),
        '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '1000')
    $playerProcess = Start-Process -FilePath $playerPath -ArgumentList $playerArguments -WorkingDirectory (Split-Path -Parent $playerPath) -WindowStyle Hidden -PassThru
    Write-Output ('Started Player x' + $speed + ', PID ' + $playerProcess.Id + ', report ' + $reportPath)
    if (-not $playerProcess.WaitForExit(660000)) { throw ('Player measurement exceeded eleven minutes; PID ' + $playerProcess.Id) }
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
