#Requires -Version 7.2
# Read-only: writes the report to stdout. Requires Windows System.Drawing.
# Reproduce from the repository root:
#   & ./docs/validation/2026-10-17-split-gun-pilot/compare-atlas-preservation.ps1
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '../../..'),
    [string]$BaselineRelativePath = '.superpowers/sdd/split-gun-pilot/before'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$baseline = Join-Path $RepositoryRoot $BaselineRelativePath
$art = 'Assets/PaperDefense/Resources/Art'
$source = 'ArtSource/Notebook/NewStyle/sprites.source.json'
$beforeMetaPath = Join-Path $baseline "$art/NotebookSprites.json"
$afterMetaPath = Join-Path $RepositoryRoot "$art/NotebookSprites.json"
$beforePngPath = Join-Path $baseline "$art/NotebookAtlas.png"
$afterPngPath = Join-Path $RepositoryRoot "$art/NotebookAtlas.png"
$beforeMeta = Get-Content -Raw $beforeMetaPath | ConvertFrom-Json
$afterMeta = Get-Content -Raw $afterMetaPath | ConvertFrom-Json
$beforeSource = Get-Content -Raw (Join-Path $baseline $source) | ConvertFrom-Json
$afterSource = Get-Content -Raw (Join-Path $RepositoryRoot $source) | ConvertFrom-Json

function FileHash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function JsonValue($value) { ConvertTo-Json -InputObject $value -Depth 30 -Compress }
function PixelHash($bitmap, $entry) {
    # Atlas JSON uses bottom-left coordinates; System.Drawing uses top-left.
    $rect = [System.Drawing.Rectangle]::new([int]$entry.x,
        [int]($bitmap.Height - $entry.y - $entry.height), [int]$entry.width, [int]$entry.height)
    $crop = $bitmap.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $locked = $null
    try {
        $locked = $crop.LockBits([System.Drawing.Rectangle]::new(0, 0, $crop.Width, $crop.Height),
            [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $rowBytes = $crop.Width * 4
        $pixels = [byte[]]::new($rowBytes * $crop.Height)
        for ($row = 0; $row -lt $crop.Height; $row++) {
            [System.Runtime.InteropServices.Marshal]::Copy(
                [IntPtr]::Add($locked.Scan0, $row * $locked.Stride), $pixels, $row * $rowBytes, $rowBytes)
        }
        [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($pixels))
    }
    finally {
        if ($null -ne $locked) { $crop.UnlockBits($locked) }
        $crop.Dispose()
    }
}

$beforeBitmap = [System.Drawing.Bitmap]::new($beforePngPath)
$afterBitmap = [System.Drawing.Bitmap]::new($afterPngPath)
try {
    $current = @{}
    foreach ($entry in $afterMeta.sprites) { $current[$entry.id] = $entry }
    $beforeEntries = @($beforeMeta.sprites | Where-Object { -not $_.alias })
    $afterEntries = @($afterMeta.sprites | Where-Object { -not $_.alias })
    $comparisons = @(foreach ($entry in $beforeEntries) {
        $match = $current[$entry.id]
        if ($null -eq $match) { throw "Missing baseline sprite: $($entry.id)" }
        $beforeHash = PixelHash $beforeBitmap $entry
        $afterHash = PixelHash $afterBitmap $match
        [ordered]@{
            id = $entry.id
            width = $entry.width
            height = $entry.height
            dimensionsEqual = ($entry.width -eq $match.width -and $entry.height -eq $match.height)
            boundsEqual = ((JsonValue $entry.bounds) -ceq (JsonValue $match.bounds))
            baselinePixelsSha256 = $beforeHash
            currentPixelsSha256 = $afterHash
            pixelsEqual = ($beforeHash -ceq $afterHash)
        }
    })
    $oldSourceEntries = @{}
    foreach ($entry in $afterSource.sprites) { $oldSourceEntries[$entry.id] = $entry }
    $sourceEntriesEqual = @($beforeSource.sprites | Where-Object {
        (JsonValue $_) -cne (JsonValue $oldSourceEntries[$_.id])
    }).Count -eq 0
    $aliasesEqual = (JsonValue $beforeSource.aliases) -ceq (JsonValue $afterSource.aliases)
    $allEqual = @($comparisons | Where-Object {
        -not $_.dimensionsEqual -or -not $_.boundsEqual -or -not $_.pixelsEqual
    }).Count -eq 0
    $report = [ordered]@{
        schemaVersion = 1
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        method = 'compare-atlas-preservation.ps1; SHA-256 of decoded BGRA8 rows, top-to-bottom, without stride padding'
        baselineSnapshot = $BaselineRelativePath
        baselineRevision = $beforeMeta.revision
        currentRevision = $afterMeta.revision
        baselineAtlas = [ordered]@{ width = $beforeBitmap.Width; height = $beforeBitmap.Height; pngSha256 = (FileHash $beforePngPath); metadataSha256 = (FileHash $beforeMetaPath) }
        currentAtlas = [ordered]@{ width = $afterBitmap.Width; height = $afterBitmap.Height; pngSha256 = (FileHash $afterPngPath); metadataSha256 = (FileHash $afterMetaPath) }
        baselineCanonicalSpriteCount = $beforeEntries.Count
        currentCanonicalSpriteCount = $afterEntries.Count
        newSpriteIds = @($afterEntries | Where-Object { $_.id -notin $beforeEntries.id } | ForEach-Object { $_.id })
        allBaselineSourceEntriesEqual = $sourceEntriesEqual
        allSourceAliasesEqual = $aliasesEqual
        allBaselineCropPixelsDimensionsAndBoundsEqual = $allEqual
        verdict = $(if ($allEqual -and $sourceEntriesEqual -and $aliasesEqual) { 'pass' } else { 'fail' })
        sprites = $comparisons
    }
    ConvertTo-Json -InputObject $report -Depth 10
}
finally {
    $beforeBitmap.Dispose()
    $afterBitmap.Dispose()
}
