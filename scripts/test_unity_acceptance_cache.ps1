$ErrorActionPreference = 'Stop'
$cacheTestRoot = Split-Path -Parent $PSScriptRoot
Add-Type -Path @(
    (Join-Path $cacheTestRoot 'unity/Assets/Game/Editor/Character/AcceptancePreparationCache.cs'),
    (Join-Path $cacheTestRoot 'scripts/fixtures/acceptance_preparation_cache_harness.cs')
)
[AcceptancePreparationCacheRegression]::Run()
