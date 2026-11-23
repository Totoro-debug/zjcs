param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$Editor = 'D:/Tool/Unity/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = [IO.Path]::GetFullPath($ProjectPath)
$validationBase = [IO.Path]::GetFullPath((Join-Path $repository 'output/validation'))
if (-not $project.StartsWith($validationBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'Select an existing independent project below output/validation; the shared main project is forbidden.'
}
if (@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
}).Count -ne 0) { throw 'Do not stage into a running Unity project.' }
$work = Join-Path $validationBase ('home-relations-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
$receipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/home-relations-source-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $receipt.outputs.PSObject.Properties) {
    $source = Join-Path $repository $entry.Name
    if (-not $entry.Name.StartsWith('unity/') -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
        throw ('Home relation source differs: ' + $entry.Name)
    }
    $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
    if ((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -eq $entry.Value) { continue }
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $source -Destination $target -Force
}
$fixture = Join-Path $project 'Assets/Editor/HomeRelationValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_relation_validation.cs') -Destination (Join-Path $fixture 'HomeRelationValidation.cs') -Force
@{ name = 'HomeRelationValidation'; includePlatforms = @('Editor'); references = @(
    'Zjcs.Official.HomeRelations', 'Unity.RenderPipelines.Universal.Runtime', 'HomeRelationProbes'
) } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeRelationValidation.asmdef') -Encoding utf8
$probes = Join-Path $project 'Assets/Validation/HomeRelationProbes'
[void](New-Item -ItemType Directory -Path $probes -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_relation_probe.cs') -Destination (Join-Path $probes 'HomeRelationProbe.cs') -Force
@{ name = 'HomeRelationProbes'; references = @('Zjcs.Official.HomeRelations') } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $probes 'HomeRelationProbes.asmdef') -Encoding utf8
$env:ZJCS_HOME_RELATION_PROJECT = $project
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @(
    '-batchmode', '-muteAudio', '-job-worker-count', '2', '-gc-helper-count', '2',
    '-projectPath', ('"' + $project + '"'), '-executeMethod', 'HomeRelationValidation.Run', '-logFile', ('"' + $log + '"')
)
Write-Output ('Home relation log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^HOME_RELATIONS_PASSED:' -Quiet)) {
    throw ('Original home relation validation failed: ' + $log)
}
Write-Output 'HOME_RELATIONS_PASSED'
