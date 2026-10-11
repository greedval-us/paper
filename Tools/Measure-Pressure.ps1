param([ValidateSet(1,2,0)][int]$Speed = 0)
$ErrorActionPreference = 'Stop'
$taskProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskPlayer = Join-Path $taskProject 'Builds/Windows/PaperDefense.exe'
if (-not (Test-Path -LiteralPath $taskPlayer -PathType Leaf)) { throw "Build missing: $taskPlayer" }
$taskSpeeds = if ($Speed -eq 0) { @(1,2) } else { @($Speed) }
foreach ($taskSpeed in $taskSpeeds) {
    $taskReport = Join-Path $taskProject "docs/validation/player-pressure-x$taskSpeed.json"
    $taskLog = Join-Path $taskProject "docs/validation/player-pressure-x$taskSpeed.log"
    $taskArguments = @('-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
        '-paper-pressure', '-pressure-speed', "$taskSpeed", '-pressure-report', ('"{0}"' -f $taskReport),
        '-logFile', ('"{0}"' -f $taskLog))
    $taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArguments -WorkingDirectory $taskProject -WindowStyle Hidden -PassThru
    Write-Output "Started rendered Player x$taskSpeed, PID $($taskProcess.Id). Report: $taskReport"
    $taskProcess.WaitForExit()
    if ($taskProcess.ExitCode -ne 0) { throw "Player x$taskSpeed exited with $($taskProcess.ExitCode). Inspect $taskLog" }
    if (-not (Test-Path -LiteralPath $taskReport)) { throw "Player did not save $taskReport" }
    $taskResult = Get-Content -Raw -LiteralPath $taskReport | ConvertFrom-Json
    if (-not $taskResult.simulation.Passed) { throw "Scenario failed at x$taskSpeed" }
    $taskResult | Select-Object speed, realSeconds, discardedSimulationSeconds, allFrames, swarm3000Frames,
        @{Name='phase';Expression={$_.simulation.Phase}}, @{Name='peak';Expression={$_.simulation.PeakAliveEnemies}},
        @{Name='fingerprint';Expression={$_.simulation.Fingerprint}} | ConvertTo-Json -Depth 4
}
