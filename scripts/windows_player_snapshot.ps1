function Resolve-ZjcsWindowsPlayerSnapshot([string]$PlayerPath, [string]$Workspace, [string]$Repository) {
    $inputPlayer = Get-Item -LiteralPath $PlayerPath
    $playerDirectory = $inputPlayer.Directory
    $cache = $playerDirectory.Parent
    $validationRoot = [IO.Path]::GetFullPath((Join-Path $Repository 'output/validation')).TrimEnd('\','/')
    if ($playerDirectory.Name -ne 'Player' -or $cache.Name -notlike 'windows-release-*' -or
        $cache.Parent.FullName.TrimEnd('\','/') -ne $validationRoot) { return $inputPlayer.FullName }
    $lease = $null
    while ($null -eq $lease) {
        try { $lease = [IO.File]::Open((Join-Path $Repository 'output/windows-release.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
        catch [IO.IOException] { Start-Sleep -Seconds 1 }
    }
    try {
        $buildPath = Join-Path $cache.FullName 'build.json'
        $build = Get-Content -LiteralPath $buildPath -Raw | ConvertFrom-Json
        if ($build.commit -notmatch '^[0-9a-f]{40}$') { throw 'The cached Player has no committed build identity.' }
        $digest = (Get-FileHash -LiteralPath $buildPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $snapshot = Join-Path $Workspace ('player-build-' + $digest.Substring(0, 20))
        $ready = Join-Path $snapshot 'source-build.json'
        if (-not (Test-Path -LiteralPath $ready)) {
            if (@(Get-ChildItem -LiteralPath $playerDirectory.FullName -Recurse -Force -Attributes ReparsePoint).Count) {
                throw 'The cached Player contains linked inputs.'
            }
            New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
            Get-ChildItem -LiteralPath $playerDirectory.FullName -Force | ForEach-Object {
                Copy-Item -LiteralPath $_.FullName -Destination $snapshot -Recurse -Force
            }
            Copy-Item -LiteralPath $buildPath -Destination $ready
            Write-Host "Created committed Windows Player snapshot: $($build.commit)"
        }
        if ((Get-FileHash -LiteralPath $ready -Algorithm SHA256).Hash.ToLowerInvariant() -ne $digest) {
            throw 'The completed Player snapshot identity changed.'
        }
        return (Join-Path $snapshot $inputPlayer.Name)
    } finally { $lease.Dispose() }
}
