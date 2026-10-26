$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
function Assert-Portrait($condition, $message) { if (-not $condition) { throw $message } }
function Read-Json($relative) { Get-Content (Join-Path $root $relative) -Raw | ConvertFrom-Json }
function Sha256-File($path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
}

$manifest = Read-Json 'assets/reference/battle-hud/monster-portraits-source-manifest.json'
$battleRules = Read-Json 'unity/Assets/Official/Combat/battle-rules.json'
$battles = @(Read-Json 'unity/Assets/Official/World/Map/10/Resources/Map10/battle-runtime.json';
    Read-Json 'unity/Assets/Official/World/Map/11/Resources/Map11/battle-runtime.json')
$monsterRows = @{}
foreach ($row in $battleRules.tables.monster) { $monsterRows[[int]$row.Id] = [int]$row.IconId }
$expected = @($battles | ForEach-Object { $_.monsterRosters } | ForEach-Object { $_.Waves } | ForEach-Object { $_.Units } |
    ForEach-Object { $monsterRows[[int]$_.MonsterId] } | Where-Object { $_ -gt 0 } | Sort-Object -Unique)
Assert-Portrait (@($manifest.monsterIconIds | Sort-Object -Unique) -join ',' -eq $expected -join ',') 'Portrait manifest does not cover Map10/Map11 monster IconIds'
Assert-Portrait ($manifest.images.Count -eq $expected.Count) 'Portrait manifest image count mismatch'

foreach ($image in $manifest.images) {
    $path = Join-Path $root $image.path
    Assert-Portrait (Test-Path $path) "Portrait PNG is missing: $($image.path)"
    Assert-Portrait ((Sha256-File $path) -eq $image.sha256) "Portrait PNG hash mismatch: $($image.path)"
    $meta = Get-Content ($path + '.meta') -Raw
    Assert-Portrait ($meta -match 'textureType: 8' -and $meta -match 'internalID: 21300000') "Portrait is not imported as a Sprite: $($image.path)"
}

$asset = Get-Content (Join-Path $root 'unity/Assets/Game/Resources/Combat/OfficialBattleMonsterPortraitCatalog.asset') -Raw
foreach ($id in $expected) { Assert-Portrait ($asset -match "iconId: $id\s+sprite:") "Catalog is missing IconId $id" }

Write-Output "Battle monster portrait checks passed: $($expected.Count) Map10/Map11 IconIds, pinned bundle receipts, PNG hashes, Sprite importer settings and catalog references."
