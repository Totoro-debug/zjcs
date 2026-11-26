param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$project = (Get-Item -LiteralPath $ProjectPath).FullName.TrimEnd('\', '/')
$validation = Join-Path $repository 'output/validation'
if (-not $project.StartsWith($validation + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use an independent source project below output/validation.'
}
$ancestor = [IO.DirectoryInfo]::new($project)
while ($ancestor.FullName -ne $validation) {
    if ((Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) -or ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Worktrees and linked source projects are forbidden.'
    }
    $ancestor = $ancestor.Parent
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count) { throw 'Do not stage into a running source project.' }
$work = Split-Path $project
if (-not (Test-Path -LiteralPath (Join-Path $work '.zjcs-validation.json'))) { throw 'Use an owned validation workspace.' }
$receipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/pet-gacha-controls-native-source-manifest.json') -Raw | ConvertFrom-Json
$rows = @()
foreach ($entry in $receipt.mesh_bindings.PSObject.Properties) {
    $mesh = $entry.Value
    $meta = @($receipt.outputs.PSObject.Properties | Where-Object {
        $_.Name.EndsWith('.asset.meta') -and (Get-Content -LiteralPath (Join-Path $repository $_.Name) -Raw).Contains('guid: ' + $mesh.pointer.guid)
    })
    if ($meta.Count -ne 1) { throw 'Resolve the exact original mesh output.' }
    foreach ($relative in @($meta[0].Name.Substring(0, $meta[0].Name.Length - 5), $meta[0].Name)) {
        $source = Join-Path $repository $relative
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $receipt.outputs.$relative) {
            throw ('Original UI mesh output differs: ' + $relative)
        }
        $target = Join-Path $project $relative.Substring(6)
        [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
        Copy-Item -LiteralPath $source -Destination $target -Force
    }
    $rows += @{ guid = $mesh.pointer.guid; vertices = $mesh.vertices; indices = $mesh.indices;
        vertexHash = $mesh.vertex_stream_sha256; indexHash = $mesh.index_stream_sha256 }
}
if (-not $rows.Count) { throw 'Expected an original UI mesh.' }
$source = Join-Path $repository 'unity/Assets/Game/Editor/Home/OfficialUiMeshValidation.cs'
Copy-Item -LiteralPath $source -Destination (Join-Path $project 'Assets/Editor/OfficialUiMeshValidation.cs') -Force
Copy-Item -LiteralPath ($source + '.meta') -Destination (Join-Path $project 'Assets/Editor/OfficialUiMeshValidation.cs.meta') -Force
$fixture = Join-Path $work 'mesh-fixture.json'
$result = Join-Path $work 'mesh-result.json'
if (Test-Path -LiteralPath $result) { throw 'Keep previous validation results; use a new owned workspace.' }
@{ meshes = $rows; result = $result } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $fixture -Encoding utf8NoBOM
$previous = $env:ZJCS_UI_MESH_FIXTURE
try {
    $env:ZJCS_UI_MESH_FIXTURE = $fixture
    $process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
        '-batchmode', '-force-d3d11', '-muteAudio', '-quit', '-job-worker-count', '2', '-gc-helper-count', '2',
        '-projectPath', ('"' + $project + '"'), '-executeMethod', 'Zjcs.Editor.Home.OfficialUiMeshValidation.Validate',
        '-logFile', ('"' + (Join-Path $work 'unity.log') + '"'))
} finally { $env:ZJCS_UI_MESH_FIXTURE = $previous }
$timer = [Diagnostics.Stopwatch]::StartNew()
while (-not $process.WaitForExit(10000)) {
    if ($timer.Elapsed.TotalMinutes -gt 8 -or ((Test-Path -LiteralPath (Join-Path $work 'unity.log')) -and
        (Select-String -LiteralPath (Join-Path $work 'unity.log') -Pattern 'error CS\d+|Assertion failed' -Quiet))) {
        Stop-Process -Id $process.Id
        throw ('Native mesh source validation failed: ' + $work)
    }
}
$process.Refresh()
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $result) -or
    -not (Get-Content -LiteralPath $result -Raw | ConvertFrom-Json).success) { throw ('Native mesh source validation failed: ' + $work) }
Write-Output ('PET_GACHA_UI_MESH_PASSED: ' + $work)
