#requires -Version 7.0
param(
    [string]$BaselineCommit = 'b759182',
    [string]$UnityDataPath,
    [string]$DotnetPath = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$commit = (& git -C $repository rev-parse "$BaselineCommit^{commit}").Trim()
if ($LASTEXITCODE -ne 0) { throw 'The original commit must already exist locally; this script never downloads objects.' }
$destination = Join-Path $repository ('Temp/OriginalCoreValidation-' + $commit.Substring(0, 7) + '/run-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
[IO.Directory]::CreateDirectory($destination) | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)

if (!$UnityDataPath) {
    $version = (Select-String -LiteralPath (Join-Path $repository 'ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
    $candidates = @(
        $env:UNITY_EDITOR_DATA,
        "D:/Program/unity dounload/Editor/$version/Editor/Data",
        "$env:ProgramFiles/Unity/Hub/Editor/$version/Editor/Data"
    )
    if ($env:UNITY_EDITOR_PATH) { $candidates += Join-Path ([IO.Path]::GetDirectoryName($env:UNITY_EDITOR_PATH)) 'Data' }
    $UnityDataPath = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'NetStandard/ref/2.1.0/netstandard.dll')) } | Select-Object -First 1
    if (!$UnityDataPath) { throw 'Pass -UnityDataPath pointing to the local Unity Editor/Data directory.' }
}
$UnityDataPath = [IO.Path]::GetFullPath($UnityDataPath)
$sdk = @(& $DotnetPath --list-sdks) | ForEach-Object {
    if ($_ -match '^([^ ]+) \[(.+)\]$') { [PSCustomObject]@{ Version = $matches[1]; Root = $matches[2] } }
} | Sort-Object { [version](($_.Version -split '-')[0]) } -Descending | Select-Object -First 1
if (!$sdk) { throw 'Install a local .NET SDK or pass -DotnetPath; this script never installs packages.' }
$compiler = Join-Path $sdk.Root ($sdk.Version + '/Roslyn/bincore/csc.dll')
if (!(Test-Path -LiteralPath $compiler)) { throw "Local compiler missing: $compiler" }

function Export-GitFile([string]$RelativePath, [string]$OutputPath) {
    $start = [Diagnostics.ProcessStartInfo]::new('git')
    $start.WorkingDirectory = $repository
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.ArgumentList.Add('show')
    $start.ArgumentList.Add($commit + ':' + $RelativePath)
    $process = [Diagnostics.Process]::Start($start)
    $stream = [IO.File]::Create($OutputPath)
    try { $process.StandardOutput.BaseStream.CopyTo($stream) } finally { $stream.Dispose() }
    $failure = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "git show failed for $RelativePath : $failure" }
}
function Write-Source([string]$Path, [string]$Source) { [IO.File]::WriteAllText($Path, $Source, $utf8) }
function Replace-ExactlyOnce([string]$Source, [string]$Before, [string]$After) {
    if ($Source.IndexOf($Before, [StringComparison]::Ordinal) -lt 0 -or $Source.IndexOf($Before, [StringComparison]::Ordinal) -ne $Source.LastIndexOf($Before, [StringComparison]::Ordinal)) {
        throw 'The original harness no longer matches the documented single adaptation.'
    }
    return $Source.Replace($Before, $After)
}

$sources = [Collections.Generic.List[string]]::new()
$proof = [Collections.Generic.List[object]]::new()
$paths = & git -C $repository ls-tree -r --name-only $commit -- Assets/PaperDefense/Core
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate original Core sources.' }
foreach ($relative in $paths) {
    if (!$relative.EndsWith('.cs', [StringComparison]::Ordinal)) { continue }
    $output = Join-Path $destination ([IO.Path]::GetFileName($relative))
    Export-GitFile $relative $output
    $sources.Add($output)
    $proof.Add([ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant() })
}
foreach ($name in @('PressureScenario.cs', 'InkLoopScenario.cs')) {
    $output = Join-Path $destination $name
    Export-GitFile ('Assets/PaperDefense/Validation/' + $name) $output
    if ($name -eq 'PressureScenario.cs') {
        $text = [IO.File]::ReadAllText($output)
        $text = Replace-ExactlyOnce $text 'public PressureScenario(PressureScenarioConfig config = null)' 'public PressureScenario(PressureScenarioConfig config = null, GameBalance configuredBalance = null)'
        $text = Replace-ExactlyOnce $text 'var balance = GameBalance.CreateDefault();' 'var balance = configuredBalance == null ? GameBalance.CreateDefault() : configuredBalance.CreateSnapshot();'
        Write-Source $output $text
    }
    $sources.Add($output)
}
$originalHarness = Join-Path $destination 'RefactorValidation.original.cs.txt'
Export-GitFile 'Assets/PaperDefense/Editor/RefactorValidation.cs' $originalHarness
$original = [IO.File]::ReadAllText($originalHarness)
$observeStart = $original.IndexOf('        static void Observe(', [StringComparison]::Ordinal)
$observeEnd = $original.IndexOf('        public static string PerformanceJson()', [StringComparison]::Ordinal)
$helpersStart = $original.IndexOf('        static string Canonical(', [StringComparison]::Ordinal)
$helpersEnd = $original.IndexOf('        [Serializable] sealed class RulesEvidence', [StringComparison]::Ordinal)
if ($observeStart -lt 0 -or $observeEnd -le $observeStart -or $helpersStart -lt 0 -or $helpersEnd -le $helpersStart) { throw 'Original hash harness boundaries changed.' }
$bridge = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'OriginalCoreValidation/OldBaselineBridge.cs.txt'))
$bridge = $bridge.Replace('@@ORIGINAL_OBSERVE@@', $original.Substring($observeStart, $observeEnd - $observeStart))
$bridge = $bridge.Replace('@@ORIGINAL_HASH_HELPERS@@', $original.Substring($helpersStart, $helpersEnd - $helpersStart))
$bridgePath = Join-Path $destination 'OldBaselineBridge.cs'
Write-Source $bridgePath $bridge
$sources.Add($bridgePath)

$dll = Join-Path $destination 'PaperDefense.OriginalCore.b759182.dll'
$arguments = @('/nologo', '/target:library', '/langversion:latest', '/optimize+', '/nostdlib+', ('/out:' + $dll))
$arguments += '/reference:' + (Join-Path $UnityDataPath 'NetStandard/ref/2.1.0/netstandard.dll')
foreach ($name in @('UnityEngine.CoreModule.dll', 'UnityEngine.JSONSerializeModule.dll')) {
    $arguments += '/reference:' + (Join-Path $UnityDataPath ('Managed/UnityEngine/' + $name))
}
$arguments += $sources
$response = Join-Path $destination 'compile.rsp'
Write-Source $response (($arguments | ForEach-Object { '"' + $_ + '"' }) -join [Environment]::NewLine)
& $DotnetPath $compiler ('@' + $response)
if ($LASTEXITCODE -ne 0) { throw "Offline csc failed with exit code $LASTEXITCODE" }
$provenance = [ordered]@{
    baselineGitCommit = $commit
    core = $proof
    compiler = $compiler
    unityReferenceDirectory = $UnityDataPath
    builtDllSha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant()
    strictFieldCopy = 'Original public fields recursively copied into original declared types. Missing source fields throw; no default substitution. New metadata has no original destination field.'
    harnessAdaptations = @('Only isolated PressureScenario configuredBalance constructor acquisition changes; all original overrides and commands remain.', 'Original Observe/Canonical/Fields/StableHash are injected unchanged from git-exported RefactorValidation.')
    bridgeTemplate = 'Tools/OriginalCoreValidation/OldBaselineBridge.cs.txt'
    dllTracked = $false
}
Write-Source (Join-Path $destination 'provenance.json') ($provenance | ConvertTo-Json -Depth 10)
Write-Output "Original Core DLL: $dll"
Write-Output "Provenance: $(Join-Path $destination 'provenance.json')"
