"""Recover original bed/cart tip thresholds and bind the local income owner."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

from official_apk import Apk, ROOT
from prepare_home_auto_increase_runtime import method
from prepare_home_experience_rules import metadata, sha
from validation_workspace import require_temporary_unity_project

CODE = ROOT/'.tools/source-packages/current/884_156692/official-code'
DEST = ROOT/'unity/Assets/Game/Runtime/Official/HomeHangUpTips'
ADAPTER = ROOT/'unity/Assets/Game/Runtime/Offline/HomeHangUpUI'


def build():
    inputs, outputs = {}, {}
    for relative in ('scripts/official_apk.py','scripts/official_bundle_reader.py','scripts/official_source.py',
        'scripts/prepare_home_auto_increase_runtime.py','scripts/prepare_home_experience_rules.py','scripts/validation_workspace.py',
        'unity/Assets/Game/Runtime/Offline/Home/OfflineHomeIncomeSession.cs',
        'unity/Assets/Game/Runtime/Offline/Home/OfflineHangUpIncome.cs',
        'unity/Assets/Game/Runtime/Official/HomeSceneUiControls/HomeHangUpDirectionGuider.cs'):
        inputs[relative]=sha(ROOT/relative)
    def read(relative):
        p=CODE/relative; inputs[p.relative_to(ROOT).as_posix()]=sha(p)
        return p.read_text('utf-8-sig').replace('\r\n','\n')
    def write(path, text):
        path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text,'utf8',newline='\n')
        for p in (path,metadata(path)):outputs[p.relative_to(ROOT).as_posix()]=sha(p)
    base=read('client-source/BaseHangUpFurniture.cs')
    bed=read('client-source/BedFurniture.cs')
    settings=read('common-source/HomeSettings.cs')
    time=read('common-source/Common/TimeUtils.cs')
    assert 'return hour * 3600.0;' in method(time,'public static double HourToSecond(double hour)')
    assert 'UniTask.WaitForSeconds((float)waitTipTime, false, (PlayerLoopTiming)8, baseHangUpFurniture.mCancellationTokenDisposable.Token, false)' in base
    read('common-source/Common/HangUpInfoParser.cs')
    selected=ROOT/'assets/reference/home-repair-native-source-manifest.json'
    inputs[selected.relative_to(ROOT).as_posix()]=sha(selected)
    extracted=json.loads(selected.read_bytes())['template_selection']
    apk=Apk(reuse_current_cache=True)
    try:
        assert apk.version=='884_156692'
        # Reuse the already decoded native HomeSettings and Luban rows only
        # after matching their real packaged objects, not bundle index names.
        for key in ('settings','hang_up'):
            row=extracted[key];source=row['source']
            obj,=[o for o in apk.objects(source['bundle']) if str(o.path_id)==source['path_id']]
            assert hashlib.sha256(obj.get_raw_data()).hexdigest()==row.get('object_sha256',row.get('sha256'))
            assert apk.provenance(obj)==source
    finally:
        apk.close()
    fields=extracted['settings']['fields']
    config=dict(manifest='884_156692',settings=fields['mHangUpTipSettings'],
        expForLevelUpPercentTip=fields['mExpForLevelUpPercentTip'],
        directionGuideFocusDuration=fields['mDirectionGuideFocusDuration'],
        records=[dict(id=r['Id'],furniture=r['BindFurniture']) for r in extracted['hang_up']['rows'] if r['NeedRecordData']])
    write(ROOT/'unity/Assets/Game/Resources/Home/hangup-tips.json',json.dumps(config,separators=(',',':'))+'\n')
    prefix='// Original selected 884 bed/cart tip logic; explicit owner and scheduling boundaries.\nusing System;\nusing System.Collections.Generic;\nusing Zjcs.Official.HomeUI;\nusing Zjcs.Official.HomeSceneUiControls;\nnamespace Zjcs.Official.HomeHangUpTips\n{\n'
    methods='\n'.join(method(base,s) for s in (
        'public bool CanReceiveAward()', 'private void OnHangUpUnlock(int hangUpId)',
        'private void OnHangUpDataChanged(int hangUpId)', 'private void OnRepairFurniture(EFurniture furniture)',
        'private void OnDebugTimeOffset(long timeOffset)', 'protected void TryBeginHangUp()',
        'private void RefreshHangUpIdList()', 'private void BeginHangUp()',
        'private void GetMaxFillPercentHangUp(out int hangUpId, out long rewardCount, out long capacity)',
        'private void ResetTipLevel()'))
    methods=methods.replace('protected void TryBeginHangUp()', 'public void TryBeginHangUp()')
    methods=methods.replace('MeManager.MainPlayer','Services.MainPlayer')
    methods=methods.replace('Parsers.Instance.HangUpInfoParser.RecordHangUpIdDict','Services.RecordHangUpIdDict')
    methods=methods.replace('HomeData.PlayerData','Services.PlayerData').replace('IHomePlayerData','IHomeHangUpTipPlayer')
    methods=methods.replace('HomeData.InMyHome','Services.InMyHome').replace('HomeSettings.Value','Services.Settings')
    methods=methods.replace('TimeUtils.HourToSecond(', 'HourToSecond(')
    methods=methods.replace('TipLevelChangePublisher.Publish(', 'Services.Publish(')
    methods=methods.replace('mRewardTipSwitcher.SetState(mTipLevel)', 'Services.SetTipState(mFurniture, mTipLevel)')
    methods=methods.replace('UniTaskVoid val = HangUpTiming(waitTipTime);\n\t\t\tval.Forget();',
        'if (waitTipTime > 0) mTiming = Services.Wait((float)waitTipTime, TryBeginHangUp);')
    methods=re.sub(r'^\s*//IL_.*\n','\n',methods,flags=re.M)
    custom=method(bed,'protected override int CustomTipLevel(long rewardCount)')
    custom=custom.replace('protected override int','private int').replace('Player mainPlayer = MeManager.MainPlayer;',
        'if (mFurniture != EFurniture.Bed) return -1;\n\t\tIHomeHangUpTipPlayer mainPlayer = Services.MainPlayer;')
    custom=custom.replace('Parsers.Instance.LevelExpParser.GetLevelUpExp(1, mainPlayer.Level)',
        'Services.GetLevelUpExp(mainPlayer.Level)').replace('HomeSettings.Value','Services.Settings')
    custom=custom.replace('base.CustomTipLevel(rewardCount)','-1')
    level=method(bed,'private void OnLevelUpdate(int level)').replace('MeManager.MainPlayer','Services.MainPlayer')
    controller='''public sealed class HomeHangUpTipController : IDisposable
{
    private readonly IHomeHangUpTipServices Services;
    private readonly EFurniture mFurniture;
    private readonly List<int> mHangUpIdList = new List<int>();
    private int mTipLevel = -1;
    private IDisposable mTiming;
    private bool enabled, started, disposed;
    public int TipLevel => mTipLevel;
    public HomeHangUpTipController(EFurniture furniture, IHomeHangUpTipServices services)
    {
        mFurniture = furniture; Services = services ?? throw new ArgumentNullException(nameof(services));
    }
    public void Start()
    {
        if (disposed) throw new ObjectDisposedException(nameof(HomeHangUpTipController));
        if (started) return; started = true;
        Services.MainPlayer.OnHangUpUnlock += OnHangUpUnlock;
        Services.MainPlayer.OnRepairFurniture += OnRepairFurniture;
    }
    public void Enable()
    {
        if (disposed) throw new ObjectDisposedException(nameof(HomeHangUpTipController));
        if (enabled) return; enabled = true;
        if (mFurniture == EFurniture.Bed) Services.MainPlayer.OnLevelUpdate += OnLevelUpdate;
        Services.MainPlayer.OnDebugTimeOffset += OnDebugTimeOffset;
        Services.MainPlayer.OnHangUpDataChanged += OnHangUpDataChanged;
        TryBeginHangUp();
    }
    public void Disable()
    {
        if (!enabled) return; enabled = false;
        if (mFurniture == EFurniture.Bed) Services.MainPlayer.OnLevelUpdate -= OnLevelUpdate;
        Services.MainPlayer.OnHangUpDataChanged -= OnHangUpDataChanged;
        Services.MainPlayer.OnDebugTimeOffset -= OnDebugTimeOffset;
        StopHangUpTiming();
    }
    public void Dispose()
    {
        if (disposed) return; Disable(); StopHangUpTiming(); disposed = true;
        if (started) {
            Services.MainPlayer.OnHangUpUnlock -= OnHangUpUnlock;
            Services.MainPlayer.OnRepairFurniture -= OnRepairFurniture;
        }
    }
    private void StopHangUpTiming() { mTiming?.Dispose(); mTiming = null; }
    private static double HourToSecond(double hour) => hour * 3600.0;
'''
    write(DEST/'HomeHangUpTipController.cs',prefix+controller+methods+custom+level+'}\n}\n')
    setting_body=method(settings,'public bool TryGetHangUpTipSetting(EFurniture furnitureType, out HangUpTipSetting setting)')
    write(DEST/'HomeHangUpTipSettings.cs',prefix+'''public sealed class HomeHangUpTipSettings
{
    [Serializable] public sealed class HangUpTipSetting
    {
        public List<float> TipLevelList;
        public List<EFurniture> FurnitureList;
    }
    private readonly List<HangUpTipSetting> mHangUpTipSettings;
    private Dictionary<EFurniture, HangUpTipSetting> mTipSettings;
    public float ExpForLevelUpPercentTip { get; }
    public HomeHangUpTipSettings(List<HangUpTipSetting> settings, float percent)
    { mHangUpTipSettings = settings ?? throw new ArgumentNullException(nameof(settings)); ExpForLevelUpPercentTip = percent; }
'''+setting_body+'}\n}\n')
    write(DEST/'Zjcs.Official.HomeHangUpTips.asmdef',json.dumps(dict(name='Zjcs.Official.HomeHangUpTips',references=[
        'Zjcs.Official.HomeUI','Zjcs.Official.HomeSceneUiControls']),indent=2)+'\n')
    write(ADAPTER/'Zjcs.Offline.HomeHangUpUI.asmdef',json.dumps(dict(name='Zjcs.Offline.HomeHangUpUI',references=[
        'Zjcs.Offline.Home','Zjcs.Official.HomeHangUpTips','Zjcs.Official.HomeUI',
        'Zjcs.Official.HomeSceneUiControls','UniTask']),indent=2)+'\n')
    for folder in (DEST,ADAPTER):
        outputs[metadata(folder,True).relative_to(ROOT).as_posix()]=sha(metadata(folder,True))
    for p in (DEST/'HomeHangUpTipServices.cs',ADAPTER/'OfflineHomeHangUpTipSource.cs',ADAPTER/'OfflineHomeHangUpTipConfiguration.cs'):
        for item in (p,metadata(p)):outputs[item.relative_to(ROOT).as_posix()]=sha(item)
    for relative in ('scripts/fixtures/home_hangup_tip_checks.cs','scripts/fixtures/home_hangup_tip_ports.cs','scripts/fixtures/home_scene_ui_native_validation.cs',
        'assets/reference/home-scene-ui-native-source-manifest.json'):
        inputs[relative]=sha(ROOT/relative)
    proof=dict(manifest='884_156692',generator_sha256=sha(Path(__file__)),inputs=inputs,outputs=outputs,
        settings=extracted['settings'],hang_up=extracted['hang_up'],
        scope='Original BaseHangUpFurniture and BedFurniture tip selection, float thresholds, wait, event order and quick-receive predicate. Local income/session event ports, selected settings and original native scene guide are bound; facility reward state-switcher graphics, claim dialogs and formal HomeFeatureRuntime are separate.')
    (ROOT/'assets/reference/home-hangup-tips-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    return proof


def stage(project,proof):
    project=require_temporary_unity_project(project)
    for relative,expected in proof['outputs'].items():
        source=ROOT/relative;assert sha(source)==expected,relative
        target=project/Path(relative).relative_to('unity');target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(source,target)
    native=json.loads((ROOT/'assets/reference/home-scene-ui-native-source-manifest.json').read_bytes())
    guid=native['roots']['HomeSceneUiGuider']['guid']
    matches=[]
    for path in (ROOT/'unity/Assets/Official/Home/UI').rglob('*.prefab.meta'):
        if 'guid: '+guid in path.read_text('utf8'):matches.append(Path(str(path)[:-5]).relative_to(ROOT/'unity').as_posix())
    prefab,=matches
    assert (project/prefab).exists(),prefab
    audio,=native['audio'].values()
    source=(ROOT/'scripts/fixtures/home_scene_ui_native_validation.cs').read_text('utf-8-sig')
    source=source.replace('HomeSceneUiNative','HomeHangUpTipNative').replace('@GUIDER_PREFAB@',prefab)
    source=source.replace('HOME_SCENE_UI_NATIVE_PASSED','HOME_HANGUP_TIP_NATIVE_PASSED')
    source=source.replace('@SOUND_GUID@',audio['pointer']['guid']).replace('@SOUND_ID@',str(audio['event']['id']))
    source='using Zjcs.Official.HomeHangUpTips;\nusing Zjcs.Offline.HomeHangUpUI;\nusing Zjcs.Offline.Home;\n'+source
    source=source.replace('Parent camera/furniture services are fixtures; formal HomeUI assembly and NPC guide click remain separate.',
        'Original bed/cart tip rules now use real saved local income and selected settings. Timer, threshold, collection, reopen and lifecycle checks passed. Camera positions, income rates and quick-receive preference are controlled; formal HomeUI and claim-window graphics remain separate.')
    marker='        var guides=root.GetComponentsInChildren<HomeHangUpDirectionGuider>(true);'
    assert source.count(marker)==1
    source=source.replace(marker,(ROOT/'scripts/fixtures/home_hangup_tip_checks.cs').read_text('utf8')+'\n'+marker)
    source=source.replace('public int TipLevel=>0;','public Func<int> Tip;public int TipLevel=>Tip?.Invoke()??0;')
    source=source.replace('public SceneServices(Camera camera){world=camera;}',
        'public SceneServices(Camera camera){world=camera;}public float FocusDuration=.2f;')
    source=source.replace('public float DirectionGuideFocusDuration=>.2f;', 'public float DirectionGuideFocusDuration=>FocusDuration;')
    source=source.replace('home-scene-ui-','home-hangup-tip-')
    folder=project/'Assets/HomeHangUpTipNativeValidation';folder.mkdir(exist_ok=True)
    (folder/'HomeHangUpTipNativeValidation.cs').write_text(source,'utf8',newline='\n')
    shutil.copyfile(ROOT/'scripts/fixtures/home_hangup_tip_ports.cs',folder/'TipPorts.cs')
    refs=['Unity.ugui','Zjcs.Official.HomeSceneUiControls','Zjcs.Official.HomeScreenUI',
        'Zjcs.Official.HomeUI','Zjcs.Official.HomeFunctionMenuUI','Zjcs.Official.World.NativeCamera',
        'Zjcs.Official.WorldUI','Zjcs.Official.ProfessionUI','UniTask','spine-unity','spine-csharp',
        'Zjcs.Official.HomeHangUpTips','Zjcs.Offline.HomeHangUpUI','Zjcs.Offline.Home']
    (folder/'HomeHangUpTipNativeValidation.asmdef').write_text(json.dumps(dict(
        name='Zjcs.HomeHangUpTipNativeValidation',includePlatforms=['Editor'],references=refs),indent=2)+'\n','utf8',newline='\n')
    print('Staged original bed/cart tip owner and native guide in',project,flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(__doc__);parser.add_argument('--project',type=Path);args=parser.parse_args()
    proof=build()
    if args.project:stage(args.project,proof)
