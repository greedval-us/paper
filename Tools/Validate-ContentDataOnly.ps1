param(
    [ValidateSet('Freeze','Verify')][string]$Mode = 'Verify'
)
$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskEvidence=Join-Path $taskRoot 'docs/validation/content'
$taskFreeze=Join-Path $taskEvidence 'code-freeze.json'
function Get-TaskFiles {
    param([bool]$CodeOnly)
    $taskPaths=if($CodeOnly){
        @(& rg --files --hidden Assets Packages ProjectSettings tools | Where-Object { $_ -match '\.(cs(\.txt)?|asmdef|mjs|ps1)$' -or $_ -match '^tools[\\/]OriginalCoreValidation[\\/]' })
    } else { @(& rg --files --hidden Assets) }
    @($taskPaths | Sort-Object | ForEach-Object {
        $taskRelative=$_.Replace('\','/')
        [pscustomobject]@{Path=$taskRelative;Sha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot $_) -Algorithm SHA256).Hash.ToLowerInvariant()}
    })
}
Push-Location -LiteralPath $taskRoot
try {
    if($Mode -eq 'Freeze'){
        if(Test-Path -LiteralPath $taskFreeze){throw 'A freeze already exists. Preserve it; do not silently replace evidence.'}
        $taskRecord=[ordered]@{Utc=[DateTime]::UtcNow.ToString('O');Head=(& git rev-parse HEAD);Code=@(Get-TaskFiles $true);Assets=@(Get-TaskFiles $false)}
        New-Item -ItemType Directory -Path $taskEvidence -Force | Out-Null
        $taskRecord | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $taskFreeze -Encoding utf8
        Write-Output "Frozen $($taskRecord.Code.Count) source files."
    } else {
        $taskRecord=Get-Content -LiteralPath $taskFreeze -Raw | ConvertFrom-Json
        $taskCurrentCode=@(Get-TaskFiles $true)
        if((ConvertTo-Json @($taskRecord.Code) -Compress) -cne (ConvertTo-Json $taskCurrentCode -Compress)){throw 'Code changed after freeze: acceptance must restart with a new explicitly documented implementation version.'}
        $taskOld=@{}; foreach($taskFile in $taskRecord.Assets){$taskOld[$taskFile.Path]=$taskFile.Sha256}
        $taskCurrent=@(Get-TaskFiles $false); $taskChanges=@()
        foreach($taskFile in $taskCurrent){
            if(-not $taskOld.ContainsKey($taskFile.Path) -or $taskOld[$taskFile.Path] -cne $taskFile.Sha256){
                $taskChanges += [pscustomobject]@{Path=$taskFile.Path;Before=$taskOld[$taskFile.Path];After=$taskFile.Sha256}
            }
            $taskOld.Remove($taskFile.Path)
        }
        foreach($taskPath in $taskOld.Keys){$taskChanges += [pscustomobject]@{Path=$taskPath;Before=$taskOld[$taskPath];After=$null}}
        foreach($taskChange in $taskChanges){
            if($taskChange.Path -notmatch '^Assets/PaperDefense/Content(/|\.meta$)' -or $taskChange.Path -notmatch '\.(asset|unity|meta)$'){
                throw "A non-acceptance asset changed after freeze: $($taskChange.Path)"
            }
        }
        [ordered]@{FreezeUtc=$taskRecord.Utc;VerifiedUtc=[DateTime]::UtcNow.ToString('O');Head=$taskRecord.Head;CodeFileCount=$taskCurrentCode.Count;CodeUnchanged=$true;AssetChanges=$taskChanges;Note='Only serialized acceptance content/scene assets changed. Report generation and build products are outside the authoring source.'} |
            ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskEvidence 'data-only-diff.json') -Encoding utf8
        Write-Output "PASS: $($taskCurrentCode.Count) unchanged source files; $($taskChanges.Count) acceptance asset changes."
    }
} finally { Pop-Location }
