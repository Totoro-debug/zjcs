param(
    [Parameter(Mandatory)][string]$ProjectPath,
    [string]$PreparedWork='',
    [string]$Editor='D:/Tool/Unity/Editor/Unity.exe',
    [string]$Python='D:/miniconda3/envs/comm/python.exe'
)
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project=[IO.Path]::GetFullPath($ProjectPath)
. (Join-Path $PSScriptRoot 'assert_home_validation_project.ps1')
Assert-HomeValidationProject -Repository $repository -Project $project
if(@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"|Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($project.Replace('\','/'))
}).Count){throw 'Do not stage into a running source project.'}
if($PreparedWork){
    $work=[IO.Path]::GetFullPath($PreparedWork)
    if(-not $work.StartsWith((Join-Path $repository 'output/validation')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath (Join-Path $work '.zjcs-validation.json'))){throw 'Reuse an owned validation directory.'}
}else{
    $work=Join-Path $repository ('output/validation/treasure-promote-runtime-'+[Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $work)
    @{format=1;repository=$repository;ownerPid=$PID}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $work '.zjcs-validation.json') -Encoding utf8
}
& $Python (Join-Path $PSScriptRoot 'prepare_treasure_promote_controls.py') --check
if($LASTEXITCODE -ne 0){throw 'Original treasure promotion controls differ.'}
$wanted=@{}
foreach($name in @('treasure-promote-controls','treasure-promote-native','treasure-detail-native','treasure-detail-configuration','treasure-slot-levels')){
    $receipt=Get-Content -LiteralPath (Join-Path $repository ('assets/reference/'+$name+'-source-manifest.json')) -Raw|ConvertFrom-Json
    foreach($entry in $receipt.outputs.PSObject.Properties){if($entry.Name.StartsWith('unity/')){$wanted[$entry.Name]=$entry.Value}}
}
$native=Get-Content -LiteralPath (Join-Path $repository 'assets/reference/treasure-promote-native-source-manifest.json') -Raw|ConvertFrom-Json
foreach($folder in @('Official/TreasurePromoteUI','Official/TreasureSlotsUI','Official/CombatAppearance',
    'Official/HomeFastHangUpProgressUI','Offline/TreasureUI')){
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $repository ('unity/Assets/Game/Runtime/'+$folder)) -File -Recurse){
        $key=$file.FullName.Substring($repository.Length+1).Replace('\','/');$wanted[$key]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
foreach($file in @('Official/WorldUI/WorldNativeGradientData.cs','Official/ProfessionUI/ProfessionPlayerPropItem.cs','Official/ProfessionUI/ProfessionImageResourceBridge.cs',
    'Official/HomeHudUI/OfficialHomeTopBarAssetCatalog.cs',
    'Official/HomeCurrencyActionsUI/HomeCurrencyActionFactory.cs','Offline/HomeUI/OfflineHomeCurrencyPlayer.cs',
    'Offline/ProfessionUI/OfflineProfessionPropertyScope.cs','Offline/ProfessionUI/OfflineProfessionAppearanceBindings.cs',
    'Offline/ProfessionUI/ProfessionNativeSceneRenderer.cs','Offline/ProfessionUI/ProfessionNativeSceneRendererSettings.cs',
    'Offline/ProfessionUI/ProfessionNativeMeshBatches.cs',
    'Offline/HomeCurrencyUI/OfflineHomeCurrencyWindows.cs',
    'Presentation/Home/HomeFeatureRuntime.NativeStorage.cs','Presentation/Home/HomeNativeSceneSlots.cs')){
    foreach($suffix in @('','.meta')){
        $key='unity/Assets/Game/Runtime/'+$file+$suffix
        $wanted[$key]=(Get-FileHash -LiteralPath (Join-Path $repository $key) -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
foreach($script in $native.native_scripts.PSObject.Properties){
    foreach($suffix in @('','.meta')){
        $key=$script.Value.path+$suffix
        $wanted[$key]=(Get-FileHash -LiteralPath (Join-Path $repository $key) -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
foreach($name in @('IProfessionMaterialServices','ProfessionPropEffectPlayAgentBase','ProfessionPropEffectPlayAgent','ProfessionUseItemSliderGroup','ProfessionCommonMaterial','ProfessionRedDotMultiItem','ProfessionDebugUIHandler')){
    foreach($suffix in @('','.meta')){
        $key='unity/Assets/Game/Runtime/Official/ProfessionUI/'+$name+'.cs'+$suffix
        $wanted[$key]=(Get-FileHash -LiteralPath (Join-Path $repository $key) -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
foreach($relative in @('Editor/ProfessionUI/ProfessionPlayerAppearanceValidation.cs','Editor/ProfessionUI/ProfessionPlayerWeaponValidation.cs',
    'Editor/Home/HomeAcceptanceLauncher.cs',
    'Editor/ProfessionUI/ProfessionValidationWorkspace.cs','Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.cs',
    'Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAppearance.cs')){
    foreach($suffix in @('','.meta')){
        $key='unity/Assets/Game/'+$relative+$suffix
        $wanted[$key]=(Get-FileHash -LiteralPath (Join-Path $repository $key) -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$wanted|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $work 'stage-request.json') -Encoding utf8
@'
import hashlib,json,pathlib,re,sys
repository,work=map(pathlib.Path,sys.argv[1:]);request=work/'stage-request.json'
wanted=json.loads(request.read_text('utf-8-sig'));seen=set()
def visit(name):
    if name in seen:return
    seen.add(name);document=json.loads((repository/name).read_bytes())
    def refs(value):
        if isinstance(value,str) and value.startswith('assets/reference/') and value.endswith('.json') and (repository/value).is_file():visit(value)
        elif isinstance(value,dict):
            for key,item in value.items():refs(key);refs(item)
        elif isinstance(value,list):
            for item in value:refs(item)
    for group in ('outputs','installed_output_sha256','reused_output_sha256','binding_source_sha256'):
        rows=document.get(group,{})
        paths=rows.keys() if isinstance(rows,dict) else [row['path'] for row in rows if isinstance(row,dict) and 'path' in row]
        for path in paths:
            if path.startswith('unity/') and (repository/path).is_file():
                with (repository/path).open('rb') as stream:wanted[path]=hashlib.file_digest(stream,'sha256').hexdigest()
    refs(document)
visit('assets/reference/partner-player-portraits-source-manifest.json')
request.write_text(json.dumps(wanted,indent=2)+'\n','utf8')
(work/'portrait-source-dependencies.json').write_text(json.dumps(sorted(seen),indent=2)+'\n','utf8')
print('Coherent original portrait dependency receipts:',len(seen))
'@ | & $Python - $repository $work
if($LASTEXITCODE -ne 0){throw 'Original main-city portrait dependency closure is incomplete.'}
@'
import hashlib,json,pathlib,shutil,sys
repository,project,work=map(pathlib.Path,sys.argv[1:])
def digest(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
native=json.loads((repository/'assets/reference/treasure-promote-native-source-manifest.json').read_bytes())
for name,expected in native['inputs'].items():
    if digest(repository/name)!=expected:raise ValueError('Consumed original treasure promotion input differs: '+name)
for name,expected in json.loads((work/'stage-request.json').read_text('utf-8-sig')).items():
    source=(repository/name).resolve();target=(project/name.removeprefix('unity/')).resolve()
    if not name.startswith('unity/') or not source.is_relative_to(repository.resolve()) or not target.is_relative_to(project.resolve()):
        raise ValueError('Stage only source assets into the explicit independent project: '+name)
    if digest(source)!=expected:raise ValueError('Source differs: '+name)
    if not target.exists() or digest(target)!=expected:
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
    if digest(target)!=expected:raise ValueError('Source changed during staging: '+name)
'@ | & $Python - $repository $project $work
if($LASTEXITCODE -ne 0){throw 'Exact source staging failed.'}
Copy-Item -LiteralPath (Join-Path $repository 'assets/reference/treasure-promote-native-source-manifest.json') -Destination (Join-Path $work 'native.json') -Force
$fixture=Join-Path $project 'Assets/Editor/TreasurePromoteNativeValidation'
[void](New-Item -ItemType Directory -Path $fixture -Force)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'fixtures/treasure_promote_native_validation.cs') -Destination (Join-Path $fixture 'TreasurePromoteNativeValidation.cs') -Force
@{name='TreasurePromoteNativeValidation';includePlatforms=@('Editor');references=@(
    'Launch','Zjcs.Editor','Zjcs.Presentation.Home','Zjcs.Offline.Home','Zjcs.Offline.HomeUI',
    'Zjcs.Offline.HomeCurrencyUI','Zjcs.Offline.HomeUI','Zjcs.Offline.HomeExperienceUI','Zjcs.Offline.HomeHudUI','Zjcs.Offline.PartnersUI',
    'Zjcs.Offline.ProfessionUI','Zjcs.Offline.TreasureUI','Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Offline.CharacterCreation',
    'Zjcs.Official.ProfessionUI','Zjcs.Official.TreasureUI','Zjcs.Official.TreasureIconUI','Zjcs.Official.TreasureSlotsUI','Zjcs.Official.TreasurePromoteUI','Zjcs.Official.TreasureDetailUI',
    'Zjcs.Official.TreasureCultivation','Zjcs.Official.ItemDetailUI','Zjcs.Official.HomeCurrencyWindowsUI',
    'Zjcs.Official.HomePlayerInfoUI','Zjcs.Official.HomeHudUI','Zjcs.Official.HomeExperienceRules','Zjcs.Official.HomeSceneInput','Zjcs.Official.HomeMainUiControls',
    'Zjcs.Official.WorldUI','Zjcs.Official.World.NativeLabelUI','Unity.ugui','Unity.TextMeshPro','Unity.Newtonsoft.Json'
)}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $fixture 'TreasurePromoteNativeValidation.asmdef') -Encoding utf8
$wanted['scripts/fixtures/treasure_promote_native_validation.cs']=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'fixtures/treasure_promote_native_validation.cs') -Algorithm SHA256).Hash.ToLowerInvariant()
$staged=Get-Content -LiteralPath (Join-Path $work 'stage-request.json') -Raw|ConvertFrom-Json -AsHashtable
$staged['scripts/fixtures/treasure_promote_native_validation.cs']=$wanted['scripts/fixtures/treasure_promote_native_validation.cs']
$staged|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $work 'staged-source.json') -Encoding utf8
. (Join-Path $PSScriptRoot 'unity_internal_display.ps1')
$oldProject=$env:ZJCS_TREASURE_PROMOTE_PROJECT;$oldOutput=$env:ZJCS_TREASURE_PROMOTE_OUTPUT;$oldAutomation=$env:ZJCS_BATTLE_INPUT_AUTOMATION
try{
    $env:ZJCS_TREASURE_PROMOTE_PROJECT=$project;$env:ZJCS_TREASURE_PROMOTE_OUTPUT=$work;$env:ZJCS_BATTLE_INPUT_AUTOMATION='1'
    $log=Join-Path $work 'unity.log'
    $process=Start-UnityOnInternalDisplay -Editor $Editor -ArgumentList @('-noaudio','-job-worker-count','2','-gc-helper-count','2',
        '-projectPath',('"'+$project+'"'),'-executeMethod','TreasurePromoteNativeValidation.Run','-logFile',('"'+$log+'"'))
}finally{$env:ZJCS_TREASURE_PROMOTE_PROJECT=$oldProject;$env:ZJCS_TREASURE_PROMOTE_OUTPUT=$oldOutput;$env:ZJCS_BATTLE_INPUT_AUTOMATION=$oldAutomation}
Write-Output ('Treasure promotion PID '+$process.Id+', output: '+$work)
$timer=[Diagnostics.Stopwatch]::StartNew()
while(-not $process.WaitForExit(10000)){
    (Get-Item -LiteralPath $work).LastWriteTime=Get-Date;(Get-Item -LiteralPath (Split-Path $project)).LastWriteTime=Get-Date
    if($timer.Elapsed.TotalMinutes -gt 25 -or ((Test-Path -LiteralPath $log) -and
        (Select-String -LiteralPath $log -Pattern 'error CS\d+|Scripts have compiler errors' -Quiet))){
        Stop-Process -Id $process.Id;throw ('Original treasure promotion checks stopped: '+$log)
    }
}
$process.Refresh()
if($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $work 'result.json')) -or
    -not (Select-String -LiteralPath $log -SimpleMatch 'TREASURE_PROMOTE_NATIVE_PASSED:' -Quiet)){
    throw ('Original treasure promotion checks failed: '+$log)
}
Write-Output ('TREASURE_PROMOTE_NATIVE_PASSED: '+$work)
