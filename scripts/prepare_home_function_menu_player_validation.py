"""Exercise original menu conditions against an actual isolated character slot."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_home_function_menu_owner_validation import prepare as prepare_owner
from validation_workspace import validation_directory


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def replace(source, before, after):
    if source.count(before) != 1:
        raise ValueError('Review original controlled menu composition: ' + before)
    return source.replace(before, after)


def prepare():
    prepare_owner()
    work=validation_directory()
    previous=work/'unity/Assets/HomeFunctionMenuOwnerValidation'
    folder=work/'unity/Assets/HomeFunctionMenuPlayerValidation'
    folder.mkdir(exist_ok=True)
    for name in ('HomeFunctionMenuOwnerRunner.cs','ControlledServices.cs'):
        source=(previous/name).read_text('utf8').replace('HomeFunctionMenuOwner','HomeFunctionMenuPlayer')
        if name.endswith('Runner.cs'):
            source=replace(source,'host.Dispose(); fonts.Dispose();',
                'host.Dispose(); yield return CheckRealPlayer(canvas.transform,hudAssets.defaultFont); fonts.Dispose();')
            source=source.replace('HOME_FUNCTION_MENU_OWNER_PASSED','HOME_FUNCTION_MENU_PLAYER_PASSED')
            source=replace(source,'    MenuServices host;',
                '    MenuServices host;\n    int runtimeErrors;\n'
                '    void OnRuntimeLog(string message,string stack,LogType type) { '
                'if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeErrors++; }')
            source=replace(source,'Application.runInBackground = true; AudioListener.volume = 0;',
                'Application.runInBackground = true; AudioListener.volume = 0; Application.logMessageReceived+=OnRuntimeLog;')
            source=replace(source,'HomeFunctionMenuPlayerValidation.Finish(null);',
                'Application.logMessageReceived-=OnRuntimeLog; HomeFunctionMenuPlayerValidation.Finish('
                'runtimeErrors==0?null:new InvalidOperationException("Unity runtime errors: "+runtimeErrors));')
        else:
            source=replace(source,'public GameObject Menu;',
                'public Zjcs.Offline.HomeFunctionMenuUI.OfflineHomeFunctionMenuPlayer LocalModel; public GameObject Menu;')
            source=replace(source,'public IHomeMenuPlayer Player=>this;',
                'public IHomeMenuPlayer Player=>LocalModel??(IHomeMenuPlayer)this;')
            source=replace(source,'public IHomeHudConditionHandler ConditionHandler=>this;',
                'public IHomeHudConditionHandler ConditionHandler=>LocalModel?.ConditionHandler??this;')
            source=replace(source,'public HomeMenuFunctionLimitInfo GetFunctionLimit(FunctionType type)=>new HomeMenuFunctionLimitInfo{Condition=condition};',
                'public HomeMenuFunctionLimitInfo GetFunctionLimit(FunctionType type)=>LocalModel!=null?LocalModel.GetFunctionLimit(type):new HomeMenuFunctionLimitInfo{Condition=condition};')
            source=replace(source,'public bool IsMet(IHomeHudCondition value)=>true;',
                'public bool IsMet(IHomeHudCondition value)=>LocalModel==null||LocalModel.IsMet(value);')
        (folder/name.replace('HomeFunctionMenuOwner','HomeFunctionMenuPlayer')).write_text(source,'utf8',newline='\n')
    fixture=ROOT/'scripts/fixtures/home_function_menu_player_validation.cs'
    (folder/'RealPlayerChecks.cs').write_text('#if UNITY_EDITOR\n'+fixture.read_text('utf8')+'\n#endif\n','utf8',newline='\n')
    assembly=json.loads((previous/'Zjcs.HomeFunctionMenuOwnerValidation.asmdef').read_bytes())
    assembly['name']='Zjcs.HomeFunctionMenuPlayerValidation'
    assembly['references']+=['Zjcs.Offline.Progression','Zjcs.Offline.CharacterCreation','Zjcs.Offline.State',
        'Zjcs.Offline.HomeUI','Zjcs.Official.Partners']
    (folder/'Zjcs.HomeFunctionMenuPlayerValidation.asmdef').write_text(json.dumps(assembly,indent=2)+'\n','utf8',newline='\n')
    code=ROOT/'.tools/source-packages/current/884_156692/official-code'
    inputs=[Path(__file__),fixture,ROOT/'scripts/fixtures/home_function_menu_owner_validation.cs',
        ROOT/'scripts/fixtures/home_function_menu_native_validation.cs',
        ROOT/'assets/reference/home-rules-current-source-manifest.json',
        ROOT/'assets/reference/home-function-menu-owner-source-manifest.json',
        ROOT/'unity/Assets/Official/Home/home-rules-current.json',
        ROOT/'unity/Assets/Game/Runtime/Offline/HomeHudUI/OfflineHomeHudConditions.cs',
        ROOT/'unity/Assets/Game/Runtime/Offline/State/OfflineOpeningPolicy.cs',
        ROOT/'assets/reference/home-condition-source-manifest.json',
        ROOT/'assets/reference/home-condition-notifications-source-manifest.json',
        code/'common-source/Common/RegionCodeCondition.cs',
        code/'common-source/Common.LubanGen/RegionCodeEnum.cs',
        code/'common-source/Common/PlanesMergeActivityEnableCondition.cs',
        code/'common-source/Common/FunctionLimitInfoParser.cs',code/'common-source/Common/PlayerUtils.cs',
        code/'client-source/FriendBtn.cs',code/'client-source/TopListBtn.cs']
    runtime=ROOT/'unity/Assets/Game/Runtime/Offline/HomeFunctionMenuUI/OfflineHomeFunctionMenuPlayer.cs'
    proof=dict(manifest='884_156692',inputs={p.relative_to(ROOT).as_posix():sha(p) for p in inputs},
        runtime={p.relative_to(ROOT).as_posix():sha(p) for p in (runtime,Path(str(runtime)+'.meta'))},
        scope='Ordinary local-player function limits use the shared selected HUD condition scope. '
              'Explicit notifications only; no GM overrides or promotion transaction implementation. '
              'Native friend/ranking visibility does not authorize online functionality.')
    (ROOT/'assets/reference/home-function-menu-player-source-manifest.json').write_text(
        json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    print('Staged real local player in original function menu')


if __name__=='__main__':
    prepare()
