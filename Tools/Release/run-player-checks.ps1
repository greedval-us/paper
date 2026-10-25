param(
    [Parameter(Mandatory=$true)][string]$BuildDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$ProfilePath
)

$ErrorActionPreference = 'Stop'
$taskBuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$taskOutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$taskProfilePath = [IO.Path]::GetFullPath($ProfilePath)
$taskTemporaryPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
if (!$taskProfilePath.StartsWith($taskTemporaryPrefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Acceptance store must be inside the temporary directory.' }
$taskExecutable = Join-Path $taskBuildDirectory 'PaperDefense.exe'
if (!(Test-Path -LiteralPath $taskExecutable) -or !(Test-Path -LiteralPath $taskProfilePath)) { throw 'The built Player and explicitly seeded temporary fixture must exist.' }
[IO.Directory]::CreateDirectory($taskOutputDirectory) | Out-Null
$taskResults = [Collections.Generic.List[object]]::new()

function Invoke-PlayerCheck([string]$Name, [string[]]$Options) {
    $taskReport = Join-Path $taskOutputDirectory ($Name + '.json')
    $taskLog = Join-Path $taskOutputDirectory ($Name + '.log')
    $taskArguments = @('-paper-release-probe', ('"'+$taskReport+'"'), '-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-force-d3d11','-logFile',('"'+$taskLog+'"')) + $Options
    $taskProcess = Start-Process -FilePath $taskExecutable -WorkingDirectory $taskBuildDirectory -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
    Write-Output ("Started {0}: PID {1}" -f $Name,$taskProcess.Id)
    $taskWaits = 0
    while (!$taskProcess.WaitForExit(10000)) {
        $taskWaits++
        if ($taskWaits -ge 24) { throw "$Name did not exit within240 seconds; inspect the explicitly launched PID and its log." }
        Write-Output ("Running {0}: {1}s" -f $Name,($taskWaits*10))
    }
    if (!(Test-Path -LiteralPath $taskReport)) { throw "$Name did not produce its acceptance report; see $taskLog" }
    $taskResult = Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json
    if ($taskProcess.ExitCode -ne 0 -or !$taskResult.passed) { throw "$Name failed: exit=$($taskProcess.ExitCode), $($taskResult.error)" }
    $taskResults.Add([pscustomobject]@{name=$Name;exitCode=$taskProcess.ExitCode;passed=$taskResult.passed;journeyComplete=$taskResult.journeyComplete;lifecycleStable=$taskResult.lifecycleStable;performanceComplete=$taskResult.performanceComplete})
    Write-Output ("Passed {0}: journey={1}, lifecycle={2}" -f $Name,$taskResult.journeyComplete,$taskResult.lifecycleStable)
}

Invoke-PlayerCheck 'performance-after' @('-paper-memory-only','-paper-release-measure-only')
Invoke-PlayerCheck 'journey-memory' @('-paper-memory-only')
Invoke-PlayerCheck 'journey-temporary' @('-paper-profile-path',('"'+$taskProfilePath+'"'))
Invoke-PlayerCheck 'journey-restart' @('-paper-profile-path',('"'+$taskProfilePath+'"'),'-paper-release-verify-restart',('"'+(Join-Path $taskOutputDirectory 'journey-temporary.json')+'"'))
$taskSummary = [ordered]@{format='paper-windows-player-checks-v1';hidden=$true;gpuLimitationAccepted=$true;results=$taskResults.ToArray()}
[IO.File]::WriteAllText((Join-Path $taskOutputDirectory 'player-checks.json'),($taskSummary | ConvertTo-Json -Depth 8)+"`n",[Text.UTF8Encoding]::new($false))
Write-Output 'All four hidden Player checks passed.'
