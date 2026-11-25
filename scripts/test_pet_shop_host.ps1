param(
    [Parameter(Mandatory)][string]$Workspace,
    [Parameter(Mandatory)][string]$Project,
    [int]$TimeoutMinutes = 45
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'validation_workspace.ps1')
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspacePath = (Get-Item -LiteralPath $Workspace).FullName.TrimEnd('\','/')
if ([IO.Path]::GetDirectoryName($workspacePath) -ne (Get-ZjcsValidationRoot).TrimEnd('\','/')) { throw 'Independent validation workspace required.' }
$marker = Get-Content -LiteralPath (Join-Path $workspacePath '.zjcs-validation.json') -Raw | ConvertFrom-Json
if ($marker.format -ne 1 -or $marker.repository -ne $repository) { throw 'Wrong validation repository.' }
$projectPath = (Get-Item -LiteralPath $Project).FullName.TrimEnd('\','/')
if (-not $projectPath.StartsWith($workspacePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Independent project required.' }
$ancestor = [IO.DirectoryInfo]::new($projectPath)
while ($ancestor -and $ancestor.FullName -ne $workspacePath) {
    if (Test-Path -LiteralPath (Join-Path $ancestor.FullName '.git')) { throw 'Worktrees are not validation projects.' }
    if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked project refused.' }
    $ancestor = $ancestor.Parent
}
$fixture = Join-Path $projectPath 'Assets/Editor/PetShopHost'
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/pet_shop_host_validation.cs') -Destination (Join-Path $fixture 'PetShopHostValidation.cs')
@{
    name = 'PetShopHostValidation'
    includePlatforms = @('Editor')
    references = @('Zjcs.Editor','Zjcs.Presentation.Home','Zjcs.Presentation.CharacterCreation','Zjcs.Offline.HomeHudUI','Zjcs.Offline.CharacterCreation',
        'Zjcs.Offline.Home','Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Official.HomeUI',
        'Zjcs.Official.HomeTipDots','Unity.ugui')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $fixture 'PetShopHostValidation.asmdef') -Encoding utf8
$editor = (Get-Content -LiteralPath (Join-Path $repository 'unity-tools.json') -Raw | ConvertFrom-Json).editorExecutable
$memory = Get-CimInstance Win32_OperatingSystem
if ($memory.FreePhysicalMemory -lt 4MB -or $memory.FreeVirtualMemory -lt 8MB) {
    throw 'Not enough available memory to start Unity verification.'
}
$oldValidationDir = $env:ZJCS_VALIDATION_DIR
$oldAutomation = $env:ZJCS_BATTLE_INPUT_AUTOMATION
$oldReopen = $env:ZJCS_PET_SHOP_REOPEN
try {
    $env:ZJCS_VALIDATION_DIR = $workspacePath
    $env:ZJCS_BATTLE_INPUT_AUTOMATION = '1'
    foreach ($stage in @('initial','cold-reopen')) {
        $env:ZJCS_PET_SHOP_REOPEN = if ($stage -eq 'cold-reopen') { '1' } else { '0' }
        $log = Join-Path $workspacePath ('pet-shop-host-' + $stage + '.log')
        $arguments = @('-muteAudio','-job-worker-count','2','-gc-helper-count','2',
            '-projectPath',('"'+$projectPath+'"'),'-logFile',('"'+$log+'"'),'-executeMethod','PetShopHostValidation.Run')
        $process = Start-UnityOnInternalDisplay -Editor $editor -ArgumentList $arguments
        $process.PriorityClass = 'BelowNormal'
        Write-Output "Pet shop host $stage PID=$($process.Id); log=$log"
        $timer = [Diagnostics.Stopwatch]::StartNew()
        while (-not $process.WaitForExit(30000)) {
            if ($timer.Elapsed.TotalMinutes -gt $TimeoutMinutes -or ((Test-Path -LiteralPath $log) -and
                (Select-String -LiteralPath $log -Pattern 'error CS\d+|Assembly with name .+ already exists|Scripts have compiler errors'))) {
                Stop-Process -Id $process.Id
                throw "Pet shop host import or runtime failed: $log"
            }
        }
        $process.Refresh()
        if ($process.ExitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'PET_SHOP_HOST_PASSED')) { throw "Pet shop host validation failed: $log" }
    }
    foreach ($name in @('pet-shop-gacha-before.png','pet-shop-selected.png','pet-shop-reopened.png','pet-shop-cold-reopen.png')) {
        if (-not (Test-Path -LiteralPath (Join-Path $workspacePath $name))) { throw "Missing rendered frame: $name" }
    }
} finally {
    $env:ZJCS_VALIDATION_DIR = $oldValidationDir
    $env:ZJCS_BATTLE_INPUT_AUTOMATION = $oldAutomation
    $env:ZJCS_PET_SHOP_REOPEN = $oldReopen
}
