function Sync-CurrentHomeFurnitureValidation {
    param([Parameter(Mandatory)][string]$Repository, [Parameter(Mandatory)][string]$Project)
    $repository = [IO.Path]::GetFullPath($Repository)
    $project = [IO.Path]::GetFullPath($Project)
    $validation = [IO.Path]::GetFullPath((Join-Path $repository 'output/validation'))
    . (Join-Path $PSScriptRoot 'assert_home_validation_project.ps1')
    Assert-HomeValidationProject -Repository $repository -Project $project
    if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
    }).Count) { throw 'Do not stage into a running Unity project.' }
    $folders = [Collections.Generic.List[string]]::new()
    foreach ($kind in @('bed', 'desk', 'diningtable')) {
        $receipt = Get-Content -LiteralPath (Join-Path $repository ('assets/reference/home-' + $kind + '-native-source-manifest.json')) -Raw -Encoding utf8 | ConvertFrom-Json
        foreach ($entry in $receipt.outputs.PSObject.Properties) {
            if (-not $entry.Name.StartsWith('unity/')) { throw 'Native furniture output is outside Unity.' }
            $src = Join-Path $repository $entry.Name
            if ((Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
                throw ('Bound current furniture output changed: ' + $entry.Name)
            }
            $dst = Join-Path $project $entry.Name.Substring(6)
            if ((Test-Path -LiteralPath $dst) -and (Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash.ToLowerInvariant() -eq $entry.Value) { continue }
            [void](New-Item -ItemType Directory -Path (Split-Path $dst) -Force)
            Copy-Item -LiteralPath $src -Destination $dst -Force
        }
        $name = @{ bed = 'Bed'; desk = 'Desk'; diningtable = 'DiningTable' }[$kind]
        $prefix = 'unity/Assets/Official/Home/' + $name + '/Native/'
        foreach ($entry in $receipt.retired_previous_outputs) {
            if (-not $entry.path.StartsWith($prefix)) { throw 'Retired furniture output is outside its original closure.' }
            $dst = [IO.Path]::GetFullPath((Join-Path $project $entry.path.Substring(6)))
            if (-not $dst.StartsWith($project + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Retired furniture output escapes the validation project.'
            }
            if ($dst.EndsWith('.meta')) {
                $folder = $dst.Substring(0, $dst.Length - 5)
                if (Test-Path -LiteralPath $folder -PathType Container) { $folders.Add($folder) }
            }
            if (-not (Test-Path -LiteralPath $dst -PathType Leaf)) { continue }
            if ((Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
                $snapshot = Join-Path $repository $entry.preserved
                if ((Get-FileHash -LiteralPath $snapshot -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) {
                    throw ('The previous furniture snapshot changed: ' + $entry.path)
                }
                $utf8 = [Text.UTF8Encoding]::new($false, $true)
                $actual = $utf8.GetString([IO.File]::ReadAllBytes($dst)).Replace("`r`n", "`n")
                $expected = $utf8.GetString([IO.File]::ReadAllBytes($snapshot)).Replace("`r`n", "`n")
                if (-not [string]::Equals($actual, $expected, [StringComparison]::Ordinal)) {
                    throw ('Preserve a changed previous furniture validation asset: ' + $entry.path)
                }
            }
            Remove-Item -LiteralPath $dst
        }
    }
    foreach ($folder in @($folders | Select-Object -Unique | Sort-Object Length -Descending)) {
        if (@(Get-ChildItem -LiteralPath $folder -Force).Count -eq 0) { Remove-Item -LiteralPath $folder }
    }
    # Spine cannot create settings from its asset-import callbacks on a fresh project.
    foreach ($relative in @('Assets/Editor/SpineSettings.asset', 'Assets/Editor/SpineSettings.asset.meta')) {
        $src = Join-Path $repository ('unity/' + $relative)
        $dst = Join-Path $project $relative
        if (-not (Test-Path -LiteralPath $dst) -and (Test-Path -LiteralPath $src)) {
            [void](New-Item -ItemType Directory -Path (Split-Path $dst) -Force)
            Copy-Item -LiteralPath $src -Destination $dst
        }
    }
}
