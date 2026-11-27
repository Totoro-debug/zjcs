"""Migrate original treasure icon controllers and replacement bus through existing native item/star/red-dot modules."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from official_source import OfficialSource,ROOT

DEST=ROOT/'unity/Assets/Game/Runtime/Official/TreasureIconUI'
RECEIPT=ROOT/'assets/reference/treasure-icon-controls-source-manifest.json'
NAMES=('ItemIconTreasure','ItemIconTreasurePanel','ItemIconTreasureQualityEffect','TreasureRedDotItem',
    'TreasureReplaceInfo','ITreasureReplaceEventLisener','TreasureReplaceEventLisener','RedDotMutexItem')
def sha(raw):return hashlib.sha256(raw).hexdigest()

def shared_lifecycle(name,text):
    text=re.sub(r'^using (?:Common|Framework(?:\.DependencyInjection)?);\n','',text,flags=re.M)
    text=re.sub(r'^\s*//IL_.*\n','',text,flags=re.M)
    text=re.sub(r'\n\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n','\n',text)
    text=text.replace('PlayerUIBase','ProfessionPlayerUIBase').replace('RedDotItemBase','ProfessionRedDotItemBase')
    if name=='PlayerUIBase':
        text=text.replace('MeManager.MainPlayer != null','PlayerAvailable').replace('MeManager.MainPlayer == null','!PlayerAvailable')
        text=text.replace('MeManager.Instance != null','PlayerManagerAvailable').replace('MeManager.Instance.OnPlayerUpdate','PlayerUpdateEvent')
        at=text.index('\n{',text.index('public class'))+2
        text=text[:at]+'''
    private IProfessionUpgradeBindings mBindings;
    protected IProfessionUpgradeBindings Bindings => mBindings ?? ProfessionUpgradeBindings.Required;
    public void Configure(IProfessionUpgradeBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }
    protected virtual bool PlayerAvailable => Bindings.MainPlayer != null;
    protected virtual bool PlayerManagerAvailable => Bindings.ManagerAvailable;
    protected virtual SafeAction PlayerUpdateEvent => Bindings.OnPlayerUpdate;
'''+text[at:]
    else:
        text=text.replace('TipDotSystem tipDotSystem = TipDotSystem;','IProfessionTipDotSystem tipDotSystem = TipDotSystem;')
        text=text.replace('\tprivate bool mIsDirty;','\tprivate bool mIsDirty;\n\n    protected virtual IProfessionTipDotSystem TipDotSystem => Bindings.TipDotSystem;')
    return ('namespace Zjcs.Official.ProfessionUI\n{\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.WorldUI;\n'+text+'\n}\n').encode()

def adapt(name,text):
    text=re.sub(r'^using (?:Common|Framework(?:\.DependencyInjection|\.Storage)?|MessagePipe|R3);\s*$', '',text,flags=re.M)
    text=re.sub(r'^\s*//IL_.*$', '',text,flags=re.M)
    text=re.sub(r'\n\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n', '\n',text)
    header='using Object=UnityEngine.Object;\nusing Zjcs.Official.ProfessionUI;\nusing Zjcs.Official.WorldUI;\nusing Zjcs.Official.TreasureCultivation;\n'
    text=text.replace('StateSwitcher<','WorldStateSwitcher<').replace('public ElementType ElementType;', 'public int ElementType;')
    text=text.replace('PlayerItemDataWrap','IProfessionItemData').replace('PlayerItemParamTreasureWrap','TreasureState')
    if name=='ItemIconTreasure':
        at=text.index('\n{',text.index('public class'))+2
        text=text[:at]+'''\n    protected ITreasureIconServices Services { get; private set; }
    public void ConfigureTreasure(ITreasureIconServices services) => Services=services??throw new System.ArgumentNullException(nameof(services));
'''+text[at:]
        text=text.replace('ElementType type','int type').replace('myInfo.ElementType','Services.GetElement(myInfo.ClassId)')
        text=text.replace('playerItemDataWrap.GetParam<ItemParamTreasure, TreasureState>()','Services.GetState(playerItemDataWrap)')
        text=text.replace('myItemData.GetParam<ItemParamTreasure, TreasureState>()','Services.GetState(myItemData)')
        text=text.replace('myItemData.GetItemParamTreasure()','Services.GetState(myItemData)')
        text=text.replace('TreasureInfo obj = myItemData.Info.PartInfo as TreasureInfo;', 'int starMax = Services.GetStarMax(myItemData);')
        text=text.replace('\n\t\tint starMax = obj.StarMax;', '')
        text=text.replace('Formula.GetMaxTreasureBaseLevel(MeManager.MainPlayer.SubRank)','Services.GetMaxTreasureBaseLevel()')
    elif name=='TreasureRedDotItem':
        text=text.replace(' : RedDotItemBase',' : TreasurePlayerRedDotItemBase').replace('private DisposableBag mDisposableBag;', 'private IDisposable mSettingSubscription;')
        text=text.replace('mDisposableBag = default;', '')
        text=re.sub(r'Disposable.AddTo<IDisposable>\(SubscriberExtensions.Subscribe<TreasureClientSetting>.*?ref mDisposableBag\);', 'mSettingSubscription = Services.SubscribeSettings(OnSettingChanged);',text)
        text=text.replace('mDisposableBag.Dispose();','mSettingSubscription?.Dispose();\n        mSettingSubscription=null;')
        text=text.replace('TreasureRedDotEnum tipType;','')
        text=text.replace('MeManager.MainPlayer.TreasureRedDot(itemById, mRedDots, out tipType, PersistentState.State.AutoMergeWhenLevelUp, MeManager.MainPlayer.TreasureSlotData.AwakeAutoMergeEnable)',
            'Services.ShowTip(itemById, mRedDots, Services.AutoMergeWhenLevelUp, Services.AwakeAutoMergeEnable)')
        text=text.replace('TreasureHelper.IsItemEffectTreasurePromote(itemData)','Services.IsPromotionItem(itemData)').replace('AwardSourceData sourceData','object sourceData')
        text=text.replace('OnSettingChanged(TreasureClientSetting settings)', 'OnSettingChanged()')
    elif name=='RedDotMutexItem':
        text=text.replace(' : RedDotItemBase',' : TreasurePlayerRedDotItemBase')
        text=re.sub(r'\bRedDotItemBase\b','ProfessionRedDotItemBase',text)
    elif name=='ItemIconTreasurePanel':
        text=text.replace('myItemData.GetItemParamTreasure()', 'Services.GetState(myItemData)')
        text=text.replace('ReplaceListener', 'Services.ReplaceListener')
        text=text.replace('TreasureHelper.CanSetupTreasureSlot(MeManager.MainPlayer, ', 'Services.CanSetupTreasureSlot(')
        text=text.replace('ServiceProxyBase<PlayerServiceProxyImpl>.Instance.OperPlayerSetupTreasureSlot(', 'Services.SetupTreasureSlot(')
        text=text.replace('UIUtils.SetUIGray(GlobalUIConfig.Value, IconGameObject, isShow)', 'Services.SetUIGray(IconGameObject, isShow)')
    text=text.replace('MeManager.MainPlayer', 'Services')
    text=text.replace('Object.op_Implicit((Object)(object)mHasHinit)', 'mHasHinit!=null').replace('Object.op_Implicit((Object)(object)QualityImg)', 'QualityImg!=null')
    if any(s in text for s in ('[Inject]','MeManager','Parsers.','Formula.','ItemParamTreasure','ServiceProxyBase','UIUtils.','SubscriberExtensions','DisposableBag','GetItemParamTreasure')):
        raise ValueError('Bind original treasure icon dependency: '+name+'\n'+text)
    return '// Original treasure UI controller; explicit local bindings only.\n'+header+'namespace Zjcs.Official.TreasureUI {\n'+'\n'.join(line.rstrip() for line in text.split('\n'))+'\n}\n'

def prepare(check=False):
    source=OfficialSource();code=source.prepared_path/'official-code';proof=source.prepared_path/'code-manifest.json';inputs=[Path(__file__),proof]
    for name in ('Assembly-CSharp.dll','Console.dll'):
        path=code/'managed/decoded'/name;assert sha(path.read_bytes())==json.loads(proof.read_bytes())['assemblies'][name]['decoded_sha256'];inputs.append(path)
    outputs={}
    for name in ('PlayerUIBase','RedDotItemBase'):
        original=code/'client-source'/(name+'.cs');inputs.append(original)
        target=ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI'/('Profession'+name+'.cs')
        raw=shared_lifecycle(name,original.read_text('utf-8-sig'))
        if target.read_bytes().replace(b'\r\n',b'\n')!=raw:raise ValueError('Current original shared lifecycle differs: '+str(target))
        outputs[target]=raw
    for name in NAMES:
        original=code/'client-source'/(name+'.cs');inputs.append(original)
        outputs[DEST/(name+'.cs')]=adapt(name,original.read_text('utf-8-sig')).encode()
    for name in ('ITreasureIconServices','TreasurePlayerRedDotItemBase'):
        ports=ROOT/'scripts/templates/treasure_ui'/(name+'.cs');inputs.append(ports);outputs[DEST/ports.name]=ports.read_bytes()
    assembly=DEST/'Zjcs.Official.TreasureIconUI.asmdef'
    data=dict(name='Zjcs.Official.TreasureIconUI',references=['Zjcs.Official.TreasureUI','Zjcs.Official.ProfessionUI',
        'Zjcs.Official.WorldUI','Zjcs.Official.TreasureCultivation','Unity.ugui','Unity.TextMeshPro'])
    outputs[assembly]=(json.dumps(data,indent=2)+'\n').encode()
    for path in list(outputs):
        if not path.is_relative_to(DEST):
            outputs[Path(str(path)+'.meta')]=Path(str(path)+'.meta').read_bytes()
            continue
        token=hashlib.md5(('zjcs:treasure-icons:'+path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        importer='AssemblyDefinitionImporter' if path.suffix=='.asmdef' else 'MonoImporter'
        outputs[Path(str(path)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\n'+importer+':\n  externalObjects: {}\n').encode()
    token=hashlib.md5(b'zjcs:treasure-icons:folder').hexdigest()
    outputs[Path(str(DEST)+'.meta')]=('fileFormatVersion: 2\nguid: '+token+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
    upgrade=ROOT/'assets/reference/profession-ui/upgrade-panel-source-manifest.json'
    upgrade_before=upgrade.read_bytes();upgrade_record=json.loads(upgrade_before)
    for row in upgrade_record['ports']:
        if row['name'] in ('PlayerUIBase','RedDotItemBase'):
            row['output']['sha256']=sha(outputs[ROOT/row['output']['path']])
    upgrade_record['shared_lifecycle_adapter']=dict(manifest=source.version,
        source_manifest=RECEIPT.relative_to(ROOT).as_posix(),
        operation='Regenerate both shared base classes from current original sources. Virtual player/manager/event/tip ports retain the existing profession defaults; no lifecycle ordering changes.')
    outputs[upgrade]=(json.dumps(upgrade_record,ensure_ascii=False,indent=2)+'\n').encode()
    shared=[ROOT/'unity/Assets/Game/Runtime/Official/ProfessionUI'/name for name in ('ItemIcon.cs','ProfessionPlayerUIBase.cs','ProfessionRedDotItemBase.cs','BlessLevelTextViewChanger.cs','StarMiniInfo.cs')]
    record=dict(manifest=source.version,inputs={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in inputs},
        shared_components={p.relative_to(ROOT).as_posix():sha(p.read_bytes()) for p in shared},outputs={p.relative_to(ROOT).as_posix():sha(raw) for p,raw in outputs.items()},
        adaptations=['Complete original icon/star/level/red-dot/replace lifetimes and event order retained.',
            'Reuse ItemIcon, TreasureStarInfo, BlessLevelTextViewChanger and original shared red-dot base; lifecycle ports preserve existing profession defaults.',
            'Typed state/config/window/gray/slot ports replace global managers, database subscriptions and network requests.'])
    prior=json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else {}
    outputs[RECEIPT]=(json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode()
    for path,raw in outputs.items():
        if check:
            if not path.is_file() or path.read_bytes()!=raw:raise ValueError('Original treasure icon output differs: '+str(path))
        else:
            if path==upgrade and path.read_bytes()!=upgrade_before:raise ValueError('Shared lifecycle receipt changed during preparation')
            if path not in (RECEIPT,upgrade) and path.is_file() and path.read_bytes()!=raw and path!=assembly and sha(path.read_bytes())!=prior.get('outputs',{}).get(path.relative_to(ROOT).as_posix()):raise ValueError('Preserve another treasure icon edit: '+str(path))
            path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(raw)
    print('Verified original treasure icon and replacement controllers',flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');prepare(parser.parse_args().check)
