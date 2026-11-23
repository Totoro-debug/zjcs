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
    throw 'Select an independent Unity project below output/validation.'
}
$running = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($project.Replace('\', '/'))
})
if ($running.Count -ne 0) { throw 'Do not stage into a running validation project.' }
$work = Join-Path $validationBase ('home-entity-bindings-' + [Guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $work)
& python (Join-Path $PSScriptRoot 'prepare_home_entity_bindings.py') --validation-dir $work
if ($LASTEXITCODE -ne 0) { throw 'Original home binding input preparation failed.' }
$receipt = Get-Content -LiteralPath (Join-Path $repository 'assets/reference/home-entity-bindings-source-manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $receipt.inputs.PSObject.Properties) {
    if (-not $entry.Name.StartsWith('unity/Assets/Game/Runtime/Official/ProfessionUI/')) { continue }
    $source = Join-Path $repository $entry.Name
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
        throw ('Reused native dictionary implementation differs: ' + $entry.Name)
    }
    Copy-Item -LiteralPath $source -Destination (Join-Path $project $entry.Name.Substring('unity/'.Length)) -Force
}
foreach ($entry in $receipt.outputs.PSObject.Properties) {
    $source = Join-Path $repository $entry.Name
    if (-not $entry.Name.StartsWith('unity/') -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value) {
        throw ('Original binding runtime differs: ' + $entry.Name)
    }
    $target = Join-Path $project $entry.Name.Substring('unity/'.Length)
    [void](New-Item -ItemType Directory -Path (Split-Path $target) -Force)
    Copy-Item -LiteralPath $source -Destination $target -Force
}
$fixture = Join-Path $project 'Assets/Editor/HomeEntityBindingsValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/home_entity_bindings_validation.cs') -Destination (Join-Path $fixture 'HomeEntityBindingsValidation.cs') -Force
@{name='HomeEntityBindingsValidation';includePlatforms=@('Editor');references=@('Zjcs.Official.HomeEntityBindings','Zjcs.Official.ProfessionUI')} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'HomeEntityBindingsValidation.asmdef') -Encoding utf8
$env:ZJCS_BINDING_PROJECT = $project
$env:ZJCS_BINDING_WORK = $work
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$log = Join-Path $work 'unity.log'
$process = Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @('-batchmode','-muteAudio','-job-worker-count','2','-gc-helper-count','2',
    '-projectPath',('"'+$project+'"'),'-executeMethod','HomeEntityBindingsValidation.Run','-logFile',('"'+$log+'"'))
Write-Output ('Home entity binding log: ' + $log)
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -Pattern '^HOME_ENTITY_BINDINGS_PASSED:' -Quiet)) {
    throw ('Home entity binding validation failed: ' + $log)
}
Write-Output 'HOME_ENTITY_BINDINGS_PASSED'
